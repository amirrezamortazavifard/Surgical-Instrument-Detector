#!/usr/bin/env python3
"""
run_test_evaluation.py
Scientific evaluation script for Surgical Instrument Detector (YOLOv11l).

Executes evaluation on the genuinely independent TEST split, reports genuine metrics,
computes cryptographic model checkpoint verification (SHA-256), and exports
machine-readable JSON and CSV evaluation reports for scientific publication.
"""

import sys
import os
import json
import csv
import time
import hashlib
import platform
import argparse
from datetime import datetime, timezone
from pathlib import Path
from collections import Counter
import yaml
import torch
import ultralytics
from ultralytics import YOLO


def parse_args():
    parser = argparse.ArgumentParser(
        description="Evaluate Surgical Instrument Detector on Independent Test Split"
    )
    parser.add_argument("--model", type=str, default="YOLO_Result/weights/best.pt",
                        help="Path to trained model weights (.pt or .onnx) (default: YOLO_Result/weights/best.pt)")
    parser.add_argument("--data", type=str, default="data.yaml",
                        help="Path to dataset configuration YAML (default: data.yaml)")
    parser.add_argument("--split", type=str, default="test", choices=["test", "val"],
                        help="Dataset partition to evaluate on (default: test)")
    parser.add_argument("--conf", type=float, default=0.5,
                        help="Confidence threshold (default: 0.5)")
    parser.add_argument("--iou", type=float, default=0.7,
                        help="NMS IoU threshold (default: 0.7)")
    parser.add_argument("--imgsz", type=int, default=640,
                        help="Inference image size (default: 640)")
    parser.add_argument("--device", type=str, default=None,
                        help="Device to use for evaluation (e.g. '0', 'cpu', default: auto)")
    parser.add_argument("--output-json", type=str, default="evaluation_report.json",
                        help="Output path for machine-readable JSON evaluation report")
    parser.add_argument("--output-csv", type=str, default="evaluation_metrics.csv",
                        help="Output path for machine-readable CSV evaluation report")
    return parser.parse_args()


def compute_file_sha256(filepath: Path) -> str:
    sha256 = hashlib.sha256()
    with open(filepath, "rb") as f:
        while chunk := f.read(65536):
            sha256.update(chunk)
    return sha256.hexdigest()


def count_ground_truth_instances(data_yaml_path: Path, split: str, class_names: list):
    """Counts actual ground truth images and bounding box instances in the evaluated split."""
    with open(data_yaml_path, "r", encoding="utf-8") as f:
        data_cfg = yaml.safe_load(f)

    raw_path = data_cfg.get("path", ".")
    dataset_root = (data_yaml_path.parent / raw_path).resolve()
    split_subpath = data_cfg.get(split, f"images/{split}")
    img_dir = dataset_root / split_subpath
    lbl_dir = dataset_root / split_subpath.replace("images", "labels")

    total_images = len(list(img_dir.glob("*.jpg"))) if img_dir.exists() else 0
    class_instances = Counter()

    if lbl_dir.exists():
        for lbl_file in lbl_dir.glob("*.txt"):
            try:
                with open(lbl_file, "r", encoding="utf-8") as lf:
                    for line in lf:
                        parts = line.strip().split()
                        if parts:
                            try:
                                c = int(parts[0])
                                if 0 <= c < len(class_names):
                                    class_instances[class_names[c]] += 1
                            except ValueError:
                                pass
            except Exception:
                pass

    return total_images, dict(class_instances)


def main():
    args = parse_args()
    script_dir = Path(__file__).resolve().parent

    model_path = Path(args.model).resolve() if Path(args.model).is_absolute() else (script_dir / args.model).resolve()
    data_yaml_path = Path(args.data).resolve() if Path(args.data).is_absolute() else (script_dir / args.data).resolve()
    report_json_path = Path(args.output_json).resolve() if Path(args.output_json).is_absolute() else (script_dir / args.output_json).resolve()
    report_csv_path = Path(args.output_csv).resolve() if Path(args.output_csv).is_absolute() else (script_dir / args.output_csv).resolve()

    print("=========================================================")
    print("  SURGICAL INSTRUMENT DETECTOR - SCIENTIFIC EVALUATION   ")
    print("=========================================================")
    print(f"Timestamp:       {datetime.now(timezone.utc).isoformat()}")
    print(f"Model Path:      {model_path}")
    print(f"Data Config:     {data_yaml_path}")
    print(f"Target Split:    {args.split}")
    print(f"Confidence:      {args.conf}")
    print(f"IoU Threshold:   {args.iou}")
    print(f"Image Size:      {args.imgsz}")
    print(f"Platform:        {platform.platform()}")
    print(f"Python Version:  {platform.python_version()}")
    print(f"PyTorch Version: {torch.__version__}")
    print(f"Ultralytics:     {ultralytics.__version__}")
    print(f"CUDA Available:  {torch.cuda.is_available()}")
    if torch.cuda.is_available():
        print(f"GPU Device:      {torch.cuda.get_device_name(0)}")

    if not model_path.exists():
        print(f"\n[ERROR] Model weights not found at: {model_path}")
        sys.exit(1)

    if not data_yaml_path.exists():
        print(f"\n[ERROR] Dataset configuration file not found at: {data_yaml_path}")
        sys.exit(1)

    print("Computing cryptographic model checksum...")
    model_sha256 = compute_file_sha256(model_path)
    model_size_bytes = model_path.stat().st_size
    print(f"Model File Size: {model_size_bytes:,} bytes")
    print(f"Model SHA-256:   {model_sha256}")

    try:
        model = YOLO(str(model_path))
    except Exception as e:
        print(f"\n[ERROR] Failed to load model: {e}")
        sys.exit(1)

    class_names = [model.names[k] for k in sorted(model.names.keys())]
    print(f"Model Classes:   {class_names}")

    # Count ground truth test samples
    total_split_images, gt_class_instances = count_ground_truth_instances(data_yaml_path, args.split, class_names)
    print(f"Split Sample Count: {total_split_images} images, {sum(gt_class_instances.values())} total annotations.")

    print(f"\nExecuting genuine Ultralytics evaluation on split='{args.split}'...")
    start_time = time.time()
    try:
        metrics = model.val(
            data=str(data_yaml_path),
            split=args.split,
            conf=args.conf,
            iou=args.iou,
            imgsz=args.imgsz,
            device=args.device,
            save_json=False,
            plots=False
        )
    except Exception as e:
        print(f"\n[BLOCKER] Evaluation run failed: {e}")
        sys.exit(2)

    elapsed_seconds = time.time() - start_time

    # Collect genuinely computed metrics
    map50 = float(metrics.box.map50)
    map_all = float(metrics.box.map)
    precision = float(metrics.box.mp) if hasattr(metrics.box, 'mp') else 0.0
    recall = float(metrics.box.mr) if hasattr(metrics.box, 'mr') else 0.0

    print("\n=========================================================")
    print("                  EVALUATION RESULTS                     ")
    print("=========================================================")
    print(f"Mean Precision (mp):       {precision:.4f}")
    print(f"Mean Recall (mr):          {recall:.4f}")
    print(f"mAP@0.50:                  {map50:.4f}")
    print(f"mAP@0.50:0.95:             {map_all:.4f}")
    print(f"Elapsed Time:              {elapsed_seconds:.2f} s")

    per_class_results = {}
    class_p = getattr(metrics.box, 'p', None)
    class_r = getattr(metrics.box, 'r', None)
    class_ap50 = getattr(metrics.box, 'ap50', None)
    class_ap = getattr(metrics.box, 'ap', None)

    print("\nPer-Class Performance:")
    print(f"{'Class Name':<28} {'GT Count':<10} {'Precision':<12} {'Recall':<10} {'mAP@0.5':<10} {'mAP@0.5:0.95':<12}")
    print("-" * 84)

    for i, class_name in enumerate(class_names):
        gt_count = gt_class_instances.get(class_name, 0)
        c_p = float(class_p[i]) if class_p is not None and i < len(class_p) else 0.0
        c_r = float(class_r[i]) if class_r is not None and i < len(class_r) else 0.0
        c_ap50 = float(class_ap50[i]) if class_ap50 is not None and i < len(class_ap50) else 0.0
        c_ap = float(class_ap[i]) if class_ap is not None and i < len(class_ap) else (
            float(metrics.box.maps[i]) if hasattr(metrics.box, 'maps') and metrics.box.maps is not None and i < len(metrics.box.maps) else 0.0
        )

        per_class_results[class_name] = {
            "ground_truth_count": gt_count,
            "precision": round(c_p, 4),
            "recall": round(c_r, 4),
            "mAP50": round(c_ap50, 4),
            "mAP50_95": round(c_ap, 4)
        }
        print(f"{class_name:<28} {gt_count:<10} {c_p:<12.4f} {c_r:<10.4f} {c_ap50:<10.4f} {c_ap:<12.4f}")
    print("-" * 84)

    # Compile structured JSON report
    report_data = {
        "metadata": {
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "python_version": platform.python_version(),
            "pytorch_version": torch.__version__,
            "ultralytics_version": ultralytics.__version__,
            "cuda_available": torch.cuda.is_available(),
            "device": torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU",
            "model_path": str(model_path),
            "model_sha256": model_sha256,
            "model_size_bytes": model_size_bytes,
            "data_yaml": str(data_yaml_path),
            "split": args.split,
            "split_images_count": total_split_images,
            "total_instances_count": sum(gt_class_instances.values())
        },
        "hyperparameters": {
            "confidence_threshold": args.conf,
            "iou_threshold": args.iou,
            "image_size": args.imgsz
        },
        "aggregate_metrics": {
            "precision": round(precision, 4),
            "recall": round(recall, 4),
            "mAP50": round(map50, 4),
            "mAP50_95": round(map_all, 4),
            "elapsed_time_seconds": round(elapsed_seconds, 2)
        },
        "per_class_metrics": per_class_results,
        "speed": {
            "preprocess_ms": getattr(metrics.speed, 'get', lambda k, v: 0.0)('preprocess', 0.0) if hasattr(metrics, 'speed') and isinstance(metrics.speed, dict) else 0.0,
            "inference_ms": getattr(metrics.speed, 'get', lambda k, v: 0.0)('inference', 0.0) if hasattr(metrics, 'speed') and isinstance(metrics.speed, dict) else 0.0,
            "postprocess_ms": getattr(metrics.speed, 'get', lambda k, v: 0.0)('postprocess', 0.0) if hasattr(metrics, 'speed') and isinstance(metrics.speed, dict) else 0.0,
        }
    }

    # Save JSON report
    report_json_path.parent.mkdir(parents=True, exist_ok=True)
    with open(report_json_path, "w", encoding="utf-8") as f:
        json.dump(report_data, f, indent=2)
    print(f"\n[INFO] Machine-readable JSON report saved to: {report_json_path}")

    # Save CSV metrics
    report_csv_path.parent.mkdir(parents=True, exist_ok=True)
    with open(report_csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["class_name", "ground_truth_count", "precision", "recall", "mAP50", "mAP50_95"])
        for c_name, m_dict in per_class_results.items():
            writer.writerow([
                c_name,
                m_dict["ground_truth_count"],
                m_dict["precision"],
                m_dict["recall"],
                m_dict["mAP50"],
                m_dict["mAP50_95"]
            ])
        writer.writerow([
            "ALL_CLASSES_AGGREGATE",
            sum(gt_class_instances.values()),
            round(precision, 4),
            round(recall, 4),
            round(map50, 4),
            round(map_all, 4)
        ])
    print(f"[INFO] Machine-readable CSV report saved to:  {report_csv_path}")


if __name__ == '__main__':
    main()
