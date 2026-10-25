#!/usr/bin/env python3
"""
prepare_and_evaluate.py
Unified pipeline to prepare the dataset and execute evaluation for YOLOv11 surgical instrument detection.
"""

import sys
import shutil
import random
import argparse
from pathlib import Path
from collections import defaultdict
import yaml
from ultralytics import YOLO


def get_basenames(filepath: Path):
    basenames = []
    with open(filepath, 'r', encoding="utf-8") as f:
        for line in f:
            line = line.strip().replace("\\", "/")
            if line:
                basenames.append(Path(line).name)
    return basenames


def parse_args():
    parser = argparse.ArgumentParser(description="Prepare dataset and evaluate YOLOv11 surgical detector")
    parser.add_argument("--dataset-dir", type=str, default=None,
                        help="Path to raw source dataset (e.g. Surgical-Dataset)")
    parser.add_argument("--output-dir", type=str, default="datasets/surg",
                        help="Path to output organized YOLO dataset directory (default: datasets/surg)")
    parser.add_argument("--model", type=str, default="YOLO_Result/weights/best.pt",
                        help="Path to trained model weights for evaluation (default: YOLO_Result/weights/best.pt)")
    parser.add_argument("--data", type=str, default="data.yaml",
                        help="Path to data.yaml configuration file (default: data.yaml)")
    parser.add_argument("--val-split", type=float, default=0.15,
                        help="Validation split ratio (default: 0.15)")
    parser.add_argument("--seed", type=int, default=42,
                        help="Random seed for deterministic train/val partition (default: 42)")
    parser.add_argument("--skip-eval", action="store_true",
                        help="Skip evaluation and only prepare dataset")
    return parser.parse_args()


def main():
    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    data_yaml_path = (script_dir / args.data).resolve() if not Path(args.data).is_absolute() else Path(args.data)
    yolo_data_dir = (script_dir / args.output_dir).resolve() if not Path(args.output_dir).is_absolute() else Path(args.output_dir)
    model_path = (script_dir / args.model).resolve() if not Path(args.model).is_absolute() else Path(args.model)

    print("=== Surgical Instrument Detection: Pipeline Execution ===")
    print(f"Target Dataset Directory: {yolo_data_dir}")
    print(f"Model Path:               {model_path}")
    print(f"Validation Split:         {args.val_split}")
    print(f"Random Seed:              {args.seed}")

    if args.dataset_dir:
        dataset_dir = Path(args.dataset_dir).resolve()
        if not dataset_dir.exists():
            print(f"[ERROR] Source dataset directory does not exist: {dataset_dir}")
            sys.exit(1)

        images_dir = dataset_dir / "Images" / "All" / "images"
        labels_dir = dataset_dir / "Labels" / "label object names"
        split_dir = dataset_dir / "Test-Train Groups"

        train_file = split_dir / "train-obj_detector.txt"
        test_file = split_dir / "test-obj_detector.txt"

        if not train_file.exists() or not test_file.exists():
            train_file = split_dir / "train_new.txt"
            test_file = split_dir / "test_new.txt"

        if not train_file.exists() or not test_file.exists():
            print(f"[ERROR] Split text files not found in '{split_dir}'.")
            sys.exit(1)

        train_basenames = get_basenames(train_file)
        test_basenames = get_basenames(test_file)

        # Deterministic partition
        train_basenames_sorted = sorted(train_basenames)
        rng = random.Random(args.seed)
        rng.shuffle(train_basenames_sorted)

        split_idx = int(len(train_basenames_sorted) * (1 - args.val_split))
        splits = {
            "train": train_basenames_sorted[:split_idx],
            "val": train_basenames_sorted[split_idx:],
            "test": sorted(test_basenames)
        }

        class_counts = {"train": defaultdict(int), "val": defaultdict(int), "test": defaultdict(int)}
        image_counts = {"train": 0, "val": 0, "test": 0}

        for split_name, basenames in splits.items():
            split_img_dir = yolo_data_dir / "images" / split_name
            split_lbl_dir = yolo_data_dir / "labels" / split_name
            split_img_dir.mkdir(parents=True, exist_ok=True)
            split_lbl_dir.mkdir(parents=True, exist_ok=True)

            for basename in basenames:
                src_img = images_dir / basename
                label_basename = Path(basename).stem + ".txt"
                src_lbl = labels_dir / label_basename

                if src_img.exists() and src_lbl.exists():
                    dst_img = split_img_dir / basename
                    dst_lbl = split_lbl_dir / label_basename

                    shutil.copy2(src_img, dst_img)
                    shutil.copy2(src_lbl, dst_lbl)
                    image_counts[split_name] += 1

                    with open(src_lbl, 'r', encoding="utf-8") as lf:
                        for line in lf:
                            parts = line.strip().split()
                            if parts:
                                try:
                                    class_counts[split_name][int(parts[0])] += 1
                                except ValueError:
                                    pass

        print("\n--- DATASET PARTITION STATISTICS ---")
        for split_name in ["train", "val", "test"]:
            print(f"{split_name.upper()} Set:")
            print(f"  Images: {image_counts[split_name]}")
            print(f"  Annotations by class:")
            for cls_id in range(4):
                print(f"    Class {cls_id}: {class_counts[split_name][cls_id]}")

        # Update data.yaml with relative path
        if data_yaml_path.exists():
            with open(data_yaml_path, 'r', encoding="utf-8") as f:
                data_config = yaml.safe_load(f)
            data_config['path'] = str(Path(yolo_data_dir).relative_to(script_dir)).replace("\\", "/")
            with open(data_yaml_path, 'w', encoding="utf-8") as f:
                yaml.safe_dump(data_config, f)

    if args.skip_eval:
        print("\nData preparation finished. Skipping evaluation as requested.")
        return

    if not model_path.exists():
        print(f"\nModel not found at '{model_path}'.")
        return

    print("\n--- Running Ultralytics YOLO Test Evaluation ---")
    try:
        model = YOLO(str(model_path))
        metrics = model.val(data=str(data_yaml_path), split='test', save_json=False, conf=0.5)
        print("\n--- TEST EVALUATION SUMMARY ---")
        print(f"mAP@0.5:      {metrics.box.map50:.4f}")
        print(f"mAP@0.5:0.95: {metrics.box.map:.4f}")
    except Exception as e:
        print(f"Evaluation could not complete: {e}")


if __name__ == "__main__":
    main()
