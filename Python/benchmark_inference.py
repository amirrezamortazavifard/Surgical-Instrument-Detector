#!/usr/bin/env python3
"""
benchmark_inference.py
Repeatable benchmarking script for the YOLOv11 ONNX and PyTorch pipelines.

Measures:
- Model initialization time
- Cold-start latency (1st run)
- Preprocessing latency (letterboxing + BGR->RGB normalization)
- Inference latency (ONNX Runtime session run)
- Postprocessing latency (coordinate reversal + NMS)
- Steady-state end-to-end FPS
- Active execution provider & system hardware metadata
Uses monotonic time.perf_counter().
"""

import sys
import os
import time
import argparse
import platform
from pathlib import Path
import numpy as np
import cv2

try:
    import onnxruntime as ort
except ImportError:
    ort = None


def parse_args():
    parser = argparse.ArgumentParser(description="Benchmark YOLOv11 ONNX inference pipeline")
    parser.add_argument("--model", type=str, default="../SurgicalDetector/SurgicalDetector/best.onnx",
                        help="Path to ONNX model artifact (default: ../SurgicalDetector/SurgicalDetector/best.onnx)")
    parser.add_argument("--image", type=str, default="../Images/Test/test_image.jpg",
                        help="Path to benchmark test image (default: ../Images/Test/test_image.jpg)")
    parser.add_argument("--warmup", type=int, default=5,
                        help="Number of warm-up iterations (default: 5)")
    parser.add_argument("--iterations", type=int, default=30,
                        help="Number of steady-state measured iterations (default: 30)")
    parser.add_argument("--conf", type=float, default=0.5,
                        help="Confidence threshold (default: 0.5)")
    parser.add_argument("--iou", type=float, default=0.45,
                        help="IoU threshold (default: 0.45)")
    return parser.parse_args()


def letterbox(img, target_shape=(640, 640), fill=(114, 114, 114)):
    h, w = img.shape[:2]
    tw, th = target_shape
    scale = min(tw / w, th / h)
    nw, nh = int(round(w * scale)), int(round(h * scale))

    resized = cv2.resize(img, (nw, nh), interpolation=cv2.INTER_LINEAR)
    dw = (tw - nw) / 2.0
    dh = (th - nh) / 2.0
    top = int(round(dh - 0.1))
    bottom = int(round(dh + 0.1))
    left = int(round(dw - 0.1))
    right = int(round(dw + 0.1))

    padded = cv2.copyMakeBorder(resized, top, bottom, left, right, cv2.BORDER_CONSTANT, value=fill)
    return padded, scale, left, top


def postprocess_and_nms(output, orig_shape, scale, pad_x, pad_y, conf_thresh, iou_thresh):
    # Output shape: [1, 8, 8400]
    preds = output[0]  # [8, 8400]
    boxes = preds[:4, :].T  # [8400, 4] -> cx, cy, w, h
    scores = preds[4:, :].T  # [8400, 4]

    class_ids = np.argmax(scores, axis=1)
    confidences = np.max(scores, axis=1)

    mask = confidences >= conf_thresh
    boxes = boxes[mask]
    confidences = confidences[mask]
    class_ids = class_ids[mask]

    if len(boxes) == 0:
        return []

    # Convert cx, cy, w, h on canvas to x1, y1, w, h in original image
    cx, cy, w, h = boxes[:, 0], boxes[:, 1], boxes[:, 2], boxes[:, 3]
    x1 = (cx - (w / 2.0) - pad_x) / scale
    y1 = (cy - (h / 2.0) - pad_y) / scale
    box_w = w / scale
    box_h = h / scale

    # OpenCV NMS
    cv_boxes = [[int(x1[i]), int(y1[i]), int(box_w[i]), int(box_h[i])] for i in range(len(boxes))]
    indices = cv2.dnn.NMSBoxes(cv_boxes, confidences.tolist(), conf_thresh, iou_thresh)
    return indices


def main():
    args = parse_args()

    if ort is None:
        print("[ERROR] onnxruntime not installed in Python environment.")
        sys.exit(1)

    model_path = Path(args.model).resolve()
    image_path = Path(args.image).resolve()

    if not model_path.exists():
        print(f"[ERROR] Model file not found at: {model_path}")
        sys.exit(1)
    if not image_path.exists():
        print(f"[ERROR] Image file not found at: {image_path}")
        sys.exit(1)

    bgr = cv2.imread(str(image_path))
    orig_h, orig_w = bgr.shape[:2]

    # 1. Model Initialization
    t0 = time.perf_counter()
    session = ort.InferenceSession(str(model_path), providers=["CPUExecutionProvider"])
    init_ms = (time.perf_counter() - t0) * 1000.0

    in_name = session.get_inputs()[0].name
    out_name = session.get_outputs()[0].name
    active_provider = session.get_providers()[0]

    # 2. Cold-start run
    t0 = time.perf_counter()
    pad_img, scale, px, py = letterbox(bgr, (640, 640))
    rgb = cv2.cvtColor(pad_img, cv2.COLOR_BGR2RGB)
    blob = np.expand_dims((rgb.astype(np.float32) / 255.0).transpose(2, 0, 1), axis=0)
    raw_out = session.run([out_name], {in_name: blob})[0]
    _ = postprocess_and_nms(raw_out, (orig_w, orig_h), scale, px, py, args.conf, args.iou)
    cold_ms = (time.perf_counter() - t0) * 1000.0

    # 3. Warm-up
    for _ in range(args.warmup):
        session.run([out_name], {in_name: blob})

    # 4. Measured Runs
    prep_times = []
    infer_times = []
    post_times = []
    e2e_times = []

    for _ in range(args.iterations):
        # Preprocessing
        t1 = time.perf_counter()
        pad_img, scale, px, py = letterbox(bgr, (640, 640))
        rgb = cv2.cvtColor(pad_img, cv2.COLOR_BGR2RGB)
        blob = np.expand_dims((rgb.astype(np.float32) / 255.0).transpose(2, 0, 1), axis=0)
        t2 = time.perf_counter()

        # Inference
        raw_out = session.run([out_name], {in_name: blob})[0]
        t3 = time.perf_counter()

        # Postprocessing
        _ = postprocess_and_nms(raw_out, (orig_w, orig_h), scale, px, py, args.conf, args.iou)
        t4 = time.perf_counter()

        prep_times.append((t2 - t1) * 1000.0)
        infer_times.append((t3 - t2) * 1000.0)
        post_times.append((t4 - t3) * 1000.0)
        e2e_times.append((t4 - t1) * 1000.0)

    mean_prep = np.mean(prep_times)
    mean_infer = np.mean(infer_times)
    mean_post = np.mean(post_times)
    mean_e2e = np.mean(e2e_times)
    median_e2e = np.median(e2e_times)
    p95_e2e = np.percentile(e2e_times, 95)
    steady_fps = 1000.0 / mean_e2e if mean_e2e > 0 else 0

    print("=================================================================")
    print("      YOLOv11 ONNX INFERENCE BENCHMARK REPORT (Python/ORT)       ")
    print("=================================================================")
    print(f"Active Provider:         {active_provider}")
    print(f"Input Image Size:        {orig_w}x{orig_h}")
    print(f"Model Canvas:            640x640")
    print(f"OS / Architecture:       {platform.platform()} ({platform.machine()})")
    print(f"CPU Cores:               {os.cpu_count()} logical processors")
    print(f"Runtime:                 Python {platform.python_version()} (ONNX Runtime {ort.__version__})")
    print("-----------------------------------------------------------------")
    print(f"Model Initialization:    {init_ms:.2f} ms")
    print(f"Cold-Start Latency:      {cold_ms:.2f} ms")
    print(f"Warm-up Iterations:      {args.warmup}")
    print(f"Measured Iterations:     {args.iterations}")
    print("-----------------------------------------------------------------")
    print(f"Mean Preprocessing:      {mean_prep:.2f} ms")
    print(f"Mean ONNX Inference:     {mean_infer:.2f} ms")
    print(f"Mean Postprocessing/NMS: {mean_post:.2f} ms")
    print(f"Mean End-to-End Latency: {mean_e2e:.2f} ms")
    print(f"Median End-to-End:       {median_e2e:.2f} ms")
    print(f"P95 End-to-End:          {p95_e2e:.2f} ms")
    print(f"Min / Max Latency:       {np.min(e2e_times):.2f} ms / {np.max(e2e_times):.2f} ms")
    print(f"Steady-State Throughput: {steady_fps:.1f} FPS")
    print("=================================================================")


if __name__ == "__main__":
    main()
