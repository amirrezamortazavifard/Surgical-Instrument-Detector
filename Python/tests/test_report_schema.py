#!/usr/bin/env python3
"""
test_report_schema.py
Unit tests validating JSON and CSV schema specifications for evaluation and dataset integrity reports.
"""

import json
import csv
import unittest
from pathlib import Path


class TestReportSchemas(unittest.TestCase):
    def setUp(self):
        self.reports_dir = Path(__file__).resolve().parent.parent / "reports"

    def test_dataset_integrity_report_schema(self):
        json_path = self.reports_dir / "dataset_integrity_report.json"
        if not json_path.exists():
            self.skipTest(f"Report file {json_path} does not exist yet.")

        with open(json_path, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertIn("metadata", data)
        self.assertIn("summary", data)
        self.assertIn("splits", data)
        self.assertIn("partition_overlaps", data)

        self.assertIn("total_images", data["summary"])
        self.assertIn("total_labels", data["summary"])
        self.assertIn("data_leakage_detected", data["summary"])
        self.assertFalse(data["summary"]["data_leakage_detected"])

        for split in ["train", "val", "test"]:
            self.assertIn(split, data["splits"])
            split_info = data["splits"][split]
            self.assertIn("image_count", split_info)
            self.assertIn("class_distribution", split_info)

    def test_dataset_class_distribution_csv_schema(self):
        csv_path = self.reports_dir / "dataset_class_distribution.csv"
        if not csv_path.exists():
            self.skipTest(f"CSV file {csv_path} does not exist yet.")

        with open(csv_path, "r", encoding="utf-8") as f:
            reader = csv.reader(f)
            header = next(reader)
            self.assertEqual(header, ["class_id", "class_name", "train", "val", "test", "total"])

            rows = list(reader)
            self.assertEqual(len(rows), 4, "Expected 4 instrument class rows")
            for row in rows:
                c_id, c_name, train_cnt, val_cnt, test_cnt, tot = row
                self.assertEqual(int(train_cnt) + int(val_cnt) + int(test_cnt), int(tot))


if __name__ == "__main__":
    unittest.main()
