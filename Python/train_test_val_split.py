#!/usr/bin/env python3
"""
train_test_val_split.py
Deterministic dataset organization for surgical instrument detection.

Splits source images and annotations into train, val, and test partitions
using a fixed random seed to guarantee experimental reproducibility.
Uses pathlib.Path for cross-platform portability.
"""

import sys
import json
import shutil
import random
import argparse
from pathlib import Path
from collections import defaultdict


def parse_args():
    parser = argparse.ArgumentParser(
        description="Deterministic Train/Val/Test Splitter for Surgical Instrument Dataset"
    )
    parser.add_argument("--source", type=str, default="source_dataset/labeled surgical tools",
                        help="Path to the source dataset directory (default: source_dataset/labeled surgical tools)")
    parser.add_argument("--dest", type=str, default="datasets/surg",
                        help="Destination directory for organized train/val/test splits (default: datasets/surg)")
    parser.add_argument("--val-split", type=float, default=0.15,
                        help="Fraction of training samples to allocate to validation (default: 0.15)")
    parser.add_argument("--seed", type=int, default=42,
                        help="Random seed for deterministic train/val splitting (default: 42)")
    parser.add_argument("--report-json", type=str, default="dataset_split_report.json",
                        help="Path to save split organization report (default: dataset_split_report.json)")
    return parser.parse_args()


def get_filename_from_path(line: str) -> str:
    cleaned = line.strip().replace("\\", "/")
    return Path(cleaned).name


def main():
    args = parse_args()
    source_path = Path(args.source).resolve()
    dest_path = Path(args.dest).resolve()

    print("==========================================================")
    print("         SURGICAL INSTRUMENT DATASET PARTITIONER          ")
    print("==========================================================")
    print(f"Source Directory:      {source_path}")
    print(f"Destination Directory: {dest_path}")
    print(f"Validation Split:      {args.val_split}")
    print(f"Random Seed:           {args.seed}")

    if not source_path.exists():
        print(f"\n[ERROR] Source directory does not exist: {source_path}")
        print("Please provide a valid path via --source <path>.")
        sys.exit(1)

    # Locate split definition files
    candidate_split_dirs = [
        source_path / "Test-Train Groups",
        source_path / "test-train groups",
        source_path / "splits",
        source_path
    ]

    train_split_file = None
    test_split_file = None

    for s_dir in candidate_split_dirs:
        if not s_dir.exists():
            continue
        for train_name in ["train_new.txt", "train-obj_detector.txt", "train.txt"]:
            cand = s_dir / train_name
            if cand.exists():
                train_split_file = cand
                break
        for test_name in ["test_new.txt", "test-obj_detector.txt", "test.txt"]:
            cand = s_dir / test_name
            if cand.exists():
                test_split_file = cand
                break
        if train_split_file and test_split_file:
            break

    if not train_split_file or not test_split_file:
        print(f"\n[ERROR] Split definition text files not found in '{source_path}'.")
        print("Expected pairs such as 'train_new.txt' / 'test_new.txt' or 'train-obj_detector.txt' / 'test-obj_detector.txt'.")
        sys.exit(1)

    print(f"Using Train Split File: {train_split_file}")
    print(f"Using Test Split File:  {test_split_file}")

    with open(train_split_file, "r", encoding="utf-8") as f:
        train_filenames = {get_filename_from_path(line) for line in f if line.strip()}

    with open(test_split_file, "r", encoding="utf-8") as f:
        test_filenames = {get_filename_from_path(line) for line in f if line.strip()}

    if not train_filenames or not test_filenames:
        print("[ERROR] Split files contain no valid filenames. Aborting to avoid empty dataset creation.")
        sys.exit(1)

    print(f"Source records indexed: {len(train_filenames)} training, {len(test_filenames)} testing.")

    # Locate source images and labels
    source_images_path = None
    source_labels_path = None

    for img_cand in [source_path / "images", source_path / "Images" / "All" / "images", source_path]:
        if img_cand.exists() and any(img_cand.glob("*.jpg")):
            source_images_path = img_cand
            break

    for lbl_cand in [source_path / "labels", source_path / "Labels" / "label object names", source_path]:
        if lbl_cand.exists() and any(lbl_cand.glob("*.txt")):
            source_labels_path = lbl_cand
            break

    if not source_images_path:
        print(f"\n[ERROR] Source images directory not found within '{source_path}'.")
        sys.exit(1)
    if not source_labels_path:
        print(f"\n[ERROR] Source labels directory not found within '{source_path}'.")
        sys.exit(1)

    print(f"Source Images Dir:     {source_images_path}")
    print(f"Source Labels Dir:     {source_labels_path}")

    # Create destination partition directories
    for folder in ["train", "val", "test"]:
        (dest_path / "images" / folder).mkdir(parents=True, exist_ok=True)
        (dest_path / "labels" / folder).mkdir(parents=True, exist_ok=True)

    # Deterministic partition with fixed RNG seed
    train_list = sorted(list(train_filenames))
    rng = random.Random(args.seed)
    rng.shuffle(train_list)

    split_index = int(len(train_list) * (1 - args.val_split))
    val_filenames = set(train_list[split_index:])
    train_filenames_final = set(train_list[:split_index])

    print(f"Partition Targets:     {len(train_filenames_final)} train, {len(val_filenames)} val, {len(test_filenames)} test.")

    copied_counts = {"train": 0, "val": 0, "test": 0}
    missing_labels = 0
    missing_images = 0

    all_target_files = {
        "train": train_filenames_final,
        "val": val_filenames,
        "test": test_filenames
    }

    for split_name, filenames in all_target_files.items():
        for image_filename in sorted(filenames):
            src_image_path = source_images_path / image_filename
            label_filename = Path(image_filename).stem + ".txt"
            src_label_path = source_labels_path / label_filename

            if not src_image_path.exists():
                missing_images += 1
                continue

            if not src_label_path.exists():
                missing_labels += 1
                continue

            dst_image_path = dest_path / "images" / split_name / image_filename
            dst_label_path = dest_path / "labels" / split_name / label_filename

            shutil.copy2(src_image_path, dst_image_path)
            shutil.copy2(src_label_path, dst_label_path)
            copied_counts[split_name] += 1

    print("\n----------------- PARTITIONING SUMMARY -----------------")
    print(f" - Train images copied: {copied_counts['train']}")
    print(f" - Val images copied:   {copied_counts['val']}")
    print(f" - Test images copied:  {copied_counts['test']}")
    if missing_images > 0:
        print(f" - [WARN] Files listed in split but missing from images: {missing_images}")
    if missing_labels > 0:
        print(f" - [WARN] Images skipped due to missing label files:     {missing_labels}")

    # Generate split report
    report = {
        "source_path": str(source_path),
        "dest_path": str(dest_path),
        "validation_split": args.val_split,
        "random_seed": args.seed,
        "counts": copied_counts,
        "missing_images": missing_images,
        "missing_labels": missing_labels
    }

    if args.report_json:
        report_file = Path(args.report_json)
        report_file.parent.mkdir(parents=True, exist_ok=True)
        with open(report_file, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        print(f"Report exported to: {report_file.resolve()}")

    if sum(copied_counts.values()) == 0:
        print("\n[ERROR] Zero images were copied. Dataset partition failed.")
        sys.exit(1)

    print("\n[SUCCESS] Dataset organized deterministically.")


if __name__ == "__main__":
    main()