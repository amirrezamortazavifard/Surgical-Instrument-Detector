#!/usr/bin/env python3
"""
train.py
Reproducible training workflow for YOLOv11 surgical instrument detection.

Preserves the verified training hyperparameters (YOLO11l, 100 epochs, imgsz=640, lr0=0.01),
enforces random seed determinism, validates dataset presence before launching,
and exports complete training metadata and runtime telemetry to JSON.
"""

import sys
import os
import json
import time
import random
import platform
import argparse
from datetime import datetime, timezone
from pathlib import Path
import yaml
import numpy as np
import torch
import ultralytics
from ultralytics import YOLO


def parse_args():
    parser = argparse.ArgumentParser(
        description="Reproducible Training Pipeline for YOLOv11 Surgical Instrument Detector"
    )
    parser.add_argument("--model", type=str, default="yolo11l.pt",
                        help="Base model architecture or pretrained checkpoint (default: yolo11l.pt)")
    parser.add_argument("--data", type=str, default="data.yaml",
                        help="Path to dataset configuration YAML (default: data.yaml)")
    parser.add_argument("--epochs", type=int, default=100,
                        help="Number of training epochs (default: 100)")
    parser.add_argument("--batch", type=int, default=16,
                        help="Batch size (default: 16)")
    parser.add_argument("--imgsz", type=int, default=640,
                        help="Input image resolution in pixels (default: 640)")
    parser.add_argument("--lr0", type=float, default=0.01,
                        help="Initial learning rate (default: 0.01)")
    parser.add_argument("--lrf", type=float, default=0.01,
                        help="Final learning rate fraction (default: 0.01)")
    parser.add_argument("--patience", type=int, default=100,
                        help="Early stopping patience in epochs (default: 100)")
    parser.add_argument("--optimizer", type=str, default="auto",
                        help="Optimizer choice ('SGD', 'Adam', 'AdamW', 'auto') (default: auto)")
    parser.add_argument("--seed", type=int, default=0,
                        help="Reproducibility seed across random, numpy, and torch (default: 0)")
    parser.add_argument("--device", type=str, default=None,
                        help="CUDA device identifier (e.g. '0', 'cpu', default: auto-detect)")
    parser.add_argument("--workers", type=int, default=8,
                        help="DataLoader worker subprocesses (default: 8)")
    parser.add_argument("--project", type=str, default="runs/train",
                        help="Destination directory for training runs (default: runs/train)")
    parser.add_argument("--name", type=str, default="surgical_yolo11l",
                        help="Identifier name for the training experiment (default: surgical_yolo11l)")
    parser.add_argument("--dry-run", action="store_true",
                        help="Verify dataset and configuration without running the training loop")
    return parser.parse_args()


def set_seed(seed: int):
    """Enforces random seeds across Python, NumPy, and PyTorch runtimes."""
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed(seed)
        torch.cuda.manual_seed_all(seed)
        torch.backends.cudnn.deterministic = True
        torch.backends.cudnn.benchmark = False


def validate_environment_and_data(data_yaml_path: Path):
    if not data_yaml_path.exists():
        raise FileNotFoundError(f"Dataset YAML configuration file not found at: {data_yaml_path}")

    with open(data_yaml_path, "r", encoding="utf-8") as f:
        data_cfg = yaml.safe_load(f)

    raw_path = data_cfg.get("path", ".")
    dataset_root = (data_yaml_path.parent / raw_path).resolve()

    if not dataset_root.exists():
        raise FileNotFoundError(f"Dataset root defined in {data_yaml_path.name} not found at: {dataset_root}")

    train_img_dir = dataset_root / data_cfg.get("train", "images/train")
    val_img_dir = dataset_root / data_cfg.get("val", "images/val")

    if not train_img_dir.exists() or not any(train_img_dir.glob("*.jpg")):
        raise FileNotFoundError(f"Training images directory empty or not found: {train_img_dir}")
    if not val_img_dir.exists() or not any(val_img_dir.glob("*.jpg")):
        raise FileNotFoundError(f"Validation images directory empty or not found: {val_img_dir}")

    return dataset_root, data_cfg


def main():
    args = parse_args()
    data_yaml_path = Path(args.data).resolve()

    print("==========================================================")
    print("      YOLOv11 REPRODUCIBLE SURGICAL TRAINING PIPELINE     ")
    print("==========================================================")
    print(f"Timestamp:           {datetime.now(timezone.utc).isoformat()}")
    print(f"Base Model:          {args.model}")
    print(f"Data Config:         {data_yaml_path}")
    print(f"Epochs:              {args.epochs}")
    print(f"Batch Size:          {args.batch}")
    print(f"Image Resolution:    {args.imgsz}")
    print(f"Initial LR (lr0):    {args.lr0}")
    print(f"Patience:            {args.patience}")
    print(f"Optimizer:           {args.optimizer}")
    print(f"Deterministic Seed:  {args.seed}")
    print(f"Platform:            {platform.platform()}")
    print(f"Python Version:      {platform.python_version()}")
    print(f"PyTorch Version:     {torch.__version__}")
    print(f"Ultralytics Version: {ultralytics.__version__}")
    print(f"CUDA Available:      {torch.cuda.is_available()}")
    if torch.cuda.is_available():
        print(f"CUDA Device:         {torch.cuda.get_device_name(0)}")

    # Seed RNGs
    set_seed(args.seed)

    # Validate dataset existence
    try:
        dataset_root, data_cfg = validate_environment_and_data(data_yaml_path)
        print(f"Dataset Root:        {dataset_root}")
        print(f"Classes:             {data_cfg.get('names', [])}")
    except FileNotFoundError as e:
        print(f"\n[FATAL ERROR] {e}")
        sys.exit(1)

    # Record training metadata
    training_metadata = {
        "metadata": {
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "python_version": platform.python_version(),
            "torch_version": torch.__version__,
            "ultralytics_version": ultralytics.__version__,
            "cuda_available": torch.cuda.is_available(),
            "device_name": torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU",
            "os": platform.platform()
        },
        "hyperparameters": {
            "model": args.model,
            "data_yaml": str(data_yaml_path),
            "epochs": args.epochs,
            "batch": args.batch,
            "imgsz": args.imgsz,
            "lr0": args.lr0,
            "lrf": args.lrf,
            "patience": args.patience,
            "optimizer": args.optimizer,
            "seed": args.seed,
            "deterministic": True,
            "workers": args.workers,
            "project": args.project,
            "name": args.name
        },
        "reproducibility_caveat": (
            "Exact bit-for-bit weight reproducibility across different GPU hardware architectures "
            "or non-deterministic CUDA atomic operations is not guaranteed by PyTorch/cuDNN. "
            "However, algorithmic seed locking ensures statistical reproducibility within standard variance."
        )
    }

    run_dir = Path(args.project) / args.name
    run_dir.mkdir(parents=True, exist_ok=True)
    meta_path = run_dir / "train_config.json"
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(training_metadata, f, indent=2)
    print(f"\nTraining configuration logged to: {meta_path.resolve()}")

    if args.dry_run:
        print("\n[SUCCESS] Dry-run completed. Configuration and dataset paths validated.")
        return

    print("\nInitializing model and launching training loop...")
    try:
        model = YOLO(args.model)
        results = model.train(
            data=str(data_yaml_path),
            epochs=args.epochs,
            batch=args.batch,
            imgsz=args.imgsz,
            lr0=args.lr0,
            lrf=args.lrf,
            patience=args.patience,
            optimizer=args.optimizer,
            seed=args.seed,
            deterministic=True,
            device=args.device,
            workers=args.workers,
            project=args.project,
            name=args.name,
            exist_ok=True,
            save=True,
            plots=True
        )
        print("\n[SUCCESS] Training session completed.")
    except Exception as e:
        print(f"\n[ERROR] Training terminated with exception: {e}")
        sys.exit(1)


if __name__ == '__main__':
    main()