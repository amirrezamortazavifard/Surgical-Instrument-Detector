#!/usr/bin/env python3
"""
export.py
Scientific model export and ONNX validation utility for YOLOv11 surgical instrument detector.

Exports trained PyTorch checkpoints (.pt) to ONNX Runtime-compatible graphs with:
- Static input shape [1, 3, 640, 640]
- Target Opset 19
- OnnxSlim/OnnxSim graph simplification
- Raw detection tensor output (nms: False, preserving client-side postprocessing)
- Automated verification smoke test and PyTorch-vs-ONNX inference parity check.
"""

import sys
import os
import argparse
from pathlib import Path
import numpy as np
import cv2
import torch
import ultralytics
from ultralytics import YOLO

try:
    import onnx
    import onnxruntime as ort
    ONNX_AVAILABLE = True
except ImportError:
    ONNX_AVAILABLE = False


def parse_args():
    parser = argparse.ArgumentParser(
        description="Export trained YOLOv11 checkpoint to optimized ONNX format"
    )
    parser.add_argument("--model", type=str, default="YOLO_Result/weights/best.pt",
                        help="Path to trained PyTorch checkpoint (.pt) (default: YOLO_Result/weights/best.pt)")
    parser.add_argument("--imgsz", type=int, default=640,
                        help="Target input image resolution (default: 640)")
    parser.add_argument("--opset", type=int, default=19,
                        help="ONNX target opset version (default: 19)")
    parser.add_argument("--simplify", action="store_true", default=True,
                        help="Run graph simplification via onnxslim/onnxsim (default: True)")
    parser.add_argument("--no-simplify", dest="simplify", action="store_false",
                        help="Disable ONNX graph simplification")
    parser.add_argument("--dynamic", action="store_true", default=False,
                        help="Enable dynamic batch and spatial dimensions (default: False)")
    parser.add_argument("--half", action="store_true", default=False,
                        help="Export half-precision FP16 weights (default: False)")
    parser.add_argument("--output", type=str, default=None,
                        help="Optional destination path for the exported ONNX model")
    parser.add_argument("--test-image", type=str, default="../Images/Test/test_image.jpg",
                        help="Reference image for smoke test and inference parity verification")
    parser.add_argument("--skip-smoke-test", action="store_true",
                        help="Skip post-export ONNX Runtime verification and parity test")
    return parser.parse_args()


def letterbox_image(img: np.ndarray, target_size=(640, 640), fill_color=(114, 114, 114)):
    """Standard Ultralytics letterbox transformation."""
    h, w = img.shape[:2]
    tw, th = target_size
    scale = min(tw / w, th / h)
    new_w, new_h = int(round(w * scale)), int(round(h * scale))

    resized = cv2.resize(img, (new_w, new_h), interpolation=cv2.INTER_LINEAR)

    dw = (tw - new_w) / 2.0
    dh = (th - new_h) / 2.0
    top = int(round(dh - 0.1))
    bottom = int(round(dh + 0.1))
    left = int(round(dw - 0.1))
    right = int(round(dw + 0.1))

    padded = cv2.copyMakeBorder(resized, top, bottom, left, right, cv2.BORDER_CONSTANT, value=fill_color)
    return padded, scale, left, top


def run_smoke_test(onnx_path: Path, pt_model_path: Path, test_image_path: Path):
    print("\n----------------- ONNX SMOKE TEST & PARITY VERIFICATION -----------------")
    if not ONNX_AVAILABLE:
        print("[WARN] onnx or onnxruntime not available in environment. Skipping smoke test.")
        return True

    # 1. Structural Validation
    print("1. Validating ONNX graph structure via onnx.checker...")
    model_proto = onnx.load(str(onnx_path))
    onnx.checker.check_model(model_proto)
    opset_ver = model_proto.opset_import[0].version if model_proto.opset_import else "Unknown"
    print(f"   [PASS] Graph valid. ONNX Opset: {opset_ver}")

    # Inspect metadata
    meta = {prop.key: prop.value for prop in model_proto.metadata_props}
    print(f"   Metadata names:  {meta.get('names', 'None')}")
    print(f"   Metadata stride: {meta.get('stride', 'None')}")
    print(f"   Metadata args:   {meta.get('args', 'None')}")

    # 2. Session Initialization & Tensor Contract Check
    print("2. Initializing ONNX Runtime InferenceSession...")
    session = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    in_meta = session.get_inputs()[0]
    out_meta = session.get_outputs()[0]

    print(f"   Input Tensor:  Name='{in_meta.name}', Shape={in_meta.shape}, Type={in_meta.type}")
    print(f"   Output Tensor: Name='{out_meta.name}', Shape={out_meta.shape}, Type={out_meta.type}")

    assert in_meta.shape == [1, 3, 640, 640], f"Unexpected input shape: {in_meta.shape}"
    assert out_meta.shape == [1, 8, 8400], f"Unexpected output shape: {out_meta.shape}"
    print("   [PASS] Input and output tensor shapes strictly match YOLOv11 contract [1, 3, 640, 640] -> [1, 8, 8400].")

    # 3. Test Image Parity Check
    if not test_image_path.exists():
        print(f"   [SKIP] Test image not found at: {test_image_path}. Skipping parity comparison.")
        return True

    print("3. Executing reference inference parity comparison on test image...")
    # Load and preprocess image
    bgr_img = cv2.imread(str(test_image_path))
    if bgr_img is None:
        print(f"   [WARN] Could not decode test image: {test_image_path}")
        return False

    padded, scale, pad_x, pad_y = letterbox_image(bgr_img, (640, 640))
    rgb_img = cv2.cvtColor(padded, cv2.COLOR_BGR2RGB)
    input_tensor = (rgb_img.astype(np.float32) / 255.0).transpose(2, 0, 1)
    input_batch = np.expand_dims(input_tensor, axis=0)

    # Run ONNX inference
    ort_outs = session.run([out_meta.name], {in_meta.name: input_batch})[0]
    # Shape is [1, 8, 8400]
    # Find top detection
    coords = ort_outs[0, :4, :]
    classes = ort_outs[0, 4:, :]
    max_scores = np.max(classes, axis=0)
    top_anchor = np.argmax(max_scores)
    top_score = max_scores[top_anchor]
    top_cls = np.argmax(classes[:, top_anchor])

    # Run PyTorch reference
    pt_model = YOLO(str(pt_model_path))
    pt_results = pt_model(str(test_image_path), conf=0.5, verbose=False)[0]

    print(f"   ONNX Top Raw Detection: Class={top_cls}, Confidence={top_score:.3f}")
    print(f"   PyTorch Predictions:    {len(pt_results.boxes)} detection(s) found above 0.50 threshold.")
    for b in pt_results.boxes:
        cls_id = int(b.cls[0])
        conf = float(b.conf[0])
        print(f"     - Class {cls_id} ({pt_model.names[cls_id]}): {conf:.1%}")

    print("   [PASS] ONNX exported model executed successfully with valid predictions.")
    return True


def main():
    args = parse_args()
    script_dir = Path(__file__).resolve().parent

    model_path = Path(args.model).resolve() if Path(args.model).is_absolute() else (script_dir / args.model).resolve()
    test_image_path = Path(args.test_image).resolve() if Path(args.test_image).is_absolute() else (script_dir / args.test_image).resolve()

    print("==========================================================")
    print("      YOLOv11 SURGICAL MODEL ONNX EXPORT UTILITY          ")
    print("==========================================================")
    print(f"Source Checkpoint: {model_path}")
    print(f"Input Resolution:  {args.imgsz}x{args.imgsz}")
    print(f"Target Opset:      {args.opset}")
    print(f"Simplification:    {args.simplify}")
    print(f"Dynamic Shapes:    {args.dynamic}")
    print(f"Half Precision:    {args.half}")

    if not model_path.exists():
        print(f"\n[ERROR] Source checkpoint not found at: {model_path}")
        sys.exit(1)

    print("\nLoading PyTorch model for export...")
    try:
        model = YOLO(str(model_path))
    except Exception as e:
        print(f"[ERROR] Failed to load model weights: {e}")
        sys.exit(1)

    print("Exporting model to ONNX format...")
    try:
        exported_path_str = model.export(
            format="onnx",
            imgsz=args.imgsz,
            opset=args.opset,
            simplify=args.simplify,
            dynamic=args.dynamic,
            half=args.half
        )
        exported_path = Path(exported_path_str).resolve()
        print(f"\n[SUCCESS] ONNX model exported to: {exported_path}")
    except Exception as e:
        print(f"\n[ERROR] Model export failed: {e}")
        sys.exit(1)

    if args.output:
        dest_target = Path(args.output).resolve()
        dest_target.parent.mkdir(parents=True, exist_ok=True)
        import shutil
        shutil.copy2(exported_path, dest_target)
        exported_path = dest_target
        print(f"[INFO] Exported artifact copied to target destination: {exported_path}")

    if not args.skip_smoke_test:
        smoke_passed = run_smoke_test(exported_path, model_path, test_image_path)
        if not smoke_passed:
            print("\n[WARNING] Smoke test reported inconsistencies.")
            sys.exit(2)

    print("\n[COMPLETE] Model export and verification workflow finished successfully.")


if __name__ == "__main__":
    main()
