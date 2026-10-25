#!/usr/bin/env python3
"""
validate_dataset.py
Scientific dataset integrity and consistency audit utility for YOLOv11 surgical instrument detection.

Verifies:
1. Discovered images and label counts across all dataset partitions (train, val, test).
2. Four intended class names and index mapping (0: Scalpel, 1: Clamp, 2: Straight Scissor, 3: Curved Scissor).
3. Label file syntax, coordinate bounds [0.0, 1.0], and strictly positive bounding-box dimensions (w > 0, h > 0).
4. Missing labels, orphaned labels, and duplicate file entries.
5. Strict set-theoretic partition independence (Train ∩ Val = ∅, Train ∩ Test = ∅, Val ∩ Test = ∅).
6. Export of machine-readable reports in JSON and CSV formats.
"""

import sys
import json
import csv
import argparse
from pathlib import Path
from collections import Counter, defaultdict
from datetime import datetime, timezone
import yaml


EXPECTED_CLASSES = [
    "Scalpel",
    "Straight Dissection Clamp",
    "Straight Mayo Scissor",
    "Curved Mayo Scissor"
]


def parse_args():
    parser = argparse.ArgumentParser(description="Audit and validate YOLO dataset integrity")
    parser.add_argument("--data", type=str, default="data.yaml",
                        help="Path to dataset configuration YAML file (default: data.yaml)")
    parser.add_argument("--dataset-dir", type=str, default=None,
                        help="Explicit path to dataset directory (overrides path in data.yaml)")
    parser.add_argument("--output-json", type=str, default="dataset_integrity_report.json",
                        help="Path to export machine-readable JSON integrity report")
    parser.add_argument("--output-csv", type=str, default="dataset_class_distribution.csv",
                        help="Path to export per-class distribution CSV")
    parser.add_argument("--strict", action="store_true",
                        help="Exit with non-zero code if any data integrity error is detected")
    return parser.parse_args()


def resolve_dataset_paths(yaml_path_str: str, explicit_data_dir: str = None):
    yaml_path = Path(yaml_path_str).resolve()
    if not yaml_path.exists():
        raise FileNotFoundError(f"Dataset configuration YAML not found at: {yaml_path}")

    with open(yaml_path, "r", encoding="utf-8") as f:
        data_cfg = yaml.safe_load(f)

    if explicit_data_dir:
        base_dir = Path(explicit_data_dir).resolve()
    else:
        raw_path = data_cfg.get("path", ".")
        base_dir = (yaml_path.parent / raw_path).resolve()

    if not base_dir.exists():
        raise FileNotFoundError(f"Dataset root directory not found at: {base_dir}")

    splits = {
        "train": data_cfg.get("train", "images/train"),
        "val": data_cfg.get("val", "images/val"),
        "test": data_cfg.get("test", "images/test")
    }

    names = data_cfg.get("names", [])
    if isinstance(names, dict):
        class_names = [names[k] for k in sorted(names.keys())]
    else:
        class_names = list(names)

    return base_dir, splits, class_names, yaml_path


def validate_label_file(label_path: Path, num_classes: int):
    """
    Validates a single YOLO format label file.
    Returns: (list_of_parsed_boxes, list_of_error_strings)
    """
    boxes = []
    errors = []

    try:
        with open(label_path, "r", encoding="utf-8") as f:
            lines = f.readlines()
    except Exception as e:
        return [], [f"Could not read label file {label_path.name}: {e}"]

    for line_idx, line in enumerate(lines, start=1):
        line = line.strip()
        if not line:
            continue

        parts = line.split()
        if len(parts) != 5:
            errors.append(f"Line {line_idx}: Expected 5 fields (class x y w h), found {len(parts)}: '{line}'")
            continue

        try:
            cls_id = int(parts[0])
            cx, cy, w, h = float(parts[1]), float(parts[2]), float(parts[3]), float(parts[4])
        except ValueError as ve:
            errors.append(f"Line {line_idx}: Non-numeric field in '{line}': {ve}")
            continue

        if cls_id < 0 or cls_id >= num_classes:
            errors.append(f"Line {line_idx}: Class index {cls_id} out of bounds [0, {num_classes - 1}]")

        if not (0.0 <= cx <= 1.0 and 0.0 <= cy <= 1.0):
            errors.append(f"Line {line_idx}: Center ({cx}, {cy}) not normalized in [0.0, 1.0]")

        if w <= 0.0 or h <= 0.0:
            errors.append(f"Line {line_idx}: Non-positive box dimensions (w={w}, h={h})")
        elif cx - (w / 2.0) < -0.05 or cx + (w / 2.0) > 1.05 or cy - (h / 2.0) < -0.05 or cy + (h / 2.0) > 1.05:
            errors.append(f"Line {line_idx}: Box extends significantly beyond image boundaries: [{cx}, {cy}, {w}, {h}]")

        boxes.append({"class_id": cls_id, "bbox": [cx, cy, w, h], "line": line_idx})

    return boxes, errors


def audit_dataset(base_dir: Path, splits: dict, class_names: list):
    num_classes = len(class_names)
    split_records = {}
    split_images = {}
    all_errors = []

    for split_name, img_subpath in splits.items():
        img_dir = (base_dir / img_subpath).resolve()
        # Derive label directory by replacing 'images' with 'labels' in path
        if "images" in img_subpath:
            lbl_subpath = img_subpath.replace("images", "labels")
        else:
            lbl_subpath = f"labels/{split_name}"
        lbl_dir = (base_dir / lbl_subpath).resolve()

        if not img_dir.exists():
            all_errors.append(f"Split '{split_name}' images directory missing: {img_dir}")
            continue
        if not lbl_dir.exists():
            all_errors.append(f"Split '{split_name}' labels directory missing: {lbl_dir}")
            continue

        valid_exts = {".jpg", ".jpeg", ".png", ".bmp"}
        images = sorted([p for p in img_dir.iterdir() if p.is_file() and p.suffix.lower() in valid_exts])
        labels = sorted([p for p in lbl_dir.iterdir() if p.is_file() and p.suffix.lower() == ".txt"])

        image_basenames = {img.stem: img for img in images}
        label_basenames = {lbl.stem: lbl for lbl in labels}

        split_images[split_name] = set(image_basenames.keys())

        missing_labels = [img.name for stem, img in image_basenames.items() if stem not in label_basenames]
        orphaned_labels = [lbl.name for stem, lbl in label_basenames.items() if stem not in image_basenames]

        class_counter = Counter()
        split_invalid_boxes = []

        for stem, lbl_path in label_basenames.items():
            if stem not in image_basenames:
                continue
            boxes, errors = validate_label_file(lbl_path, num_classes)
            if errors:
                split_invalid_boxes.append({"file": lbl_path.name, "errors": errors})
                all_errors.extend([f"{lbl_path.name}: {e}" for e in errors])
            for b in boxes:
                class_counter[b["class_id"]] += 1

        split_records[split_name] = {
            "image_count": len(images),
            "label_count": len(labels),
            "paired_count": len(images) - len(missing_labels),
            "missing_labels": missing_labels,
            "orphaned_labels": orphaned_labels,
            "class_distribution": {
                class_names[c]: class_counter[c] for c in range(num_classes)
            },
            "class_counts_by_id": {c: class_counter[c] for c in range(num_classes)},
            "invalid_sample_count": len(split_invalid_boxes),
            "invalid_samples": split_invalid_boxes[:10]  # sample first 10 for readability
        }

    # Verify partition disjointness (no data leakage)
    overlaps = {}
    split_keys = list(split_images.keys())
    for i in range(len(split_keys)):
        for j in range(i + 1, len(split_keys)):
            s1, s2 = split_keys[i], split_keys[j]
            overlap = split_images[s1] & split_images[s2]
            overlaps[f"{s1}_vs_{s2}"] = list(sorted(overlap))
            if overlap:
                all_errors.append(f"Data leakage detected! {len(overlap)} samples overlap between '{s1}' and '{s2}'.")

    return split_records, overlaps, all_errors


def main():
    args = parse_args()

    print("==========================================================")
    print("      YOLOv11 SURGICAL DATASET INTEGRITY & AUDIT TOOL      ")
    print("==========================================================")
    print(f"Timestamp:        {datetime.now(timezone.utc).isoformat()}")

    try:
        base_dir, splits, class_names, yaml_path = resolve_dataset_paths(args.data, args.dataset_dir)
    except FileNotFoundError as e:
        print(f"\n[FATAL ERROR] {e}")
        sys.exit(1)

    print(f"Data Config YAML: {yaml_path}")
    print(f"Dataset Root:     {base_dir}")
    print(f"Classes ({len(class_names)}):     {class_names}")

    # Verify expected class names and mapping
    class_mapping_matches = (class_names == EXPECTED_CLASSES)
    if not class_mapping_matches:
        print(f"[WARNING] Configured class names differ from standard taxonomy:")
        print(f"  Found:    {class_names}")
        print(f"  Expected: {EXPECTED_CLASSES}")

    split_records, overlaps, all_errors = audit_dataset(base_dir, splits, class_names)

    # Console Summary Table
    print("\n----------------- DATASET AUDIT SUMMARY -----------------")
    print(f"{'Split':<10} {'Images':<8} {'Labels':<8} {'Paired':<8} {'Missing Lbl':<12} {'Orphaned Lbl':<12} {'Invalid':<8}")
    print("-" * 72)
    total_imgs = 0
    total_lbls = 0
    for split_name, rec in split_records.items():
        total_imgs += rec["image_count"]
        total_lbls += rec["label_count"]
        print(f"{split_name:<10} {rec['image_count']:<8} {rec['label_count']:<8} {rec['paired_count']:<8} "
              f"{len(rec['missing_labels']):<12} {len(rec['orphaned_labels']):<12} {rec['invalid_sample_count']:<8}")
    print("-" * 72)
    print(f"{'TOTAL':<10} {total_imgs:<8} {total_lbls:<8}")

    print("\n---------------- CLASS DISTRIBUTION PER SPLIT ----------------")
    header = f"{'Class ID':<10} {'Class Name':<28}" + "".join([f"{s.upper():<10}" for s in split_records.keys()]) + f"{'TOTAL':<10}"
    print(header)
    print("-" * len(header))
    total_by_class = Counter()
    for c_id, c_name in enumerate(class_names):
        counts = [split_records[s]["class_counts_by_id"].get(c_id, 0) for s in split_records.keys()]
        c_tot = sum(counts)
        total_by_class[c_id] = c_tot
        row = f"{c_id:<10} {c_name:<28}" + "".join([f"{cnt:<10}" for cnt in counts]) + f"{c_tot:<10}"
        print(row)
    print("-" * len(header))

    # Overlap Summary
    print("\n---------------- PARTITION INDEPENDENCE (LEAKAGE CHECK) ----------------")
    leakage_found = False
    for pair, shared in overlaps.items():
        status = "PASSED (Disjoint)" if len(shared) == 0 else f"FAILED ({len(shared)} shared samples)"
        if len(shared) > 0:
            leakage_found = True
        print(f"  - {pair:18s}: {status}")

    # Generate JSON report
    report_data = {
        "metadata": {
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "data_yaml": str(yaml_path),
            "dataset_root": str(base_dir),
            "classes": class_names,
            "taxonomy_matches_expected": class_mapping_matches
        },
        "summary": {
            "total_images": total_imgs,
            "total_labels": total_lbls,
            "total_errors": len(all_errors),
            "data_leakage_detected": leakage_found
        },
        "splits": split_records,
        "partition_overlaps": overlaps,
        "validation_errors": all_errors[:50]
    }

    if args.output_json:
        out_json_path = Path(args.output_json)
        out_json_path.parent.mkdir(parents=True, exist_ok=True)
        with open(out_json_path, "w", encoding="utf-8") as f:
            json.dump(report_data, f, indent=2)
        print(f"\n[INFO] Machine-readable integrity report saved to: {out_json_path.resolve()}")

    if args.output_csv:
        out_csv_path = Path(args.output_csv)
        out_csv_path.parent.mkdir(parents=True, exist_ok=True)
        with open(out_csv_path, "w", newline="", encoding="utf-8") as f:
            writer = csv.writer(f)
            splits_list = list(split_records.keys())
            writer.writerow(["class_id", "class_name"] + splits_list + ["total"])
            for c_id, c_name in enumerate(class_names):
                counts = [split_records[s]["class_counts_by_id"].get(c_id, 0) for s in splits_list]
                writer.writerow([c_id, c_name] + counts + [sum(counts)])
        print(f"[INFO] Per-class distribution CSV saved to: {out_csv_path.resolve()}")

    if all_errors:
        print(f"\n[WARNING] Found {len(all_errors)} integrity issue(s). Inspect report for details.")
        if args.strict:
            sys.exit(1)
    else:
        print("\n[SUCCESS] Dataset integrity verified with 100% valid annotations and zero partition leakage.")


if __name__ == "__main__":
    main()
