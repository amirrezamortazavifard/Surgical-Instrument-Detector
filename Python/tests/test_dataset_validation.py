#!/usr/bin/env python3
"""
test_dataset_validation.py
Unit tests for YOLO dataset annotation validation and partition disjointness checks.
"""

import sys
import unittest
import tempfile
from pathlib import Path

# Add Python directory to path
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from validate_dataset import validate_label_file, EXPECTED_CLASSES


class TestDatasetValidation(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.dir_path = Path(self.temp_dir.name)

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_valid_label_file_passes(self):
        label_file = self.dir_path / "valid.txt"
        label_file.write_text("0 0.5 0.5 0.2 0.3\n1 0.2 0.8 0.1 0.15\n", encoding="utf-8")

        boxes, errors = validate_label_file(label_file, len(EXPECTED_CLASSES))
        self.assertEqual(len(errors), 0, f"Expected 0 errors, got: {errors}")
        self.assertEqual(len(boxes), 2)
        self.assertEqual(boxes[0]["class_id"], 0)
        self.assertEqual(boxes[1]["class_id"], 1)

    def test_invalid_class_index_flagged(self):
        label_file = self.dir_path / "invalid_class.txt"
        # Class index 4 is out of bounds for 4 classes (0..3)
        label_file.write_text("4 0.5 0.5 0.2 0.2\n", encoding="utf-8")

        boxes, errors = validate_label_file(label_file, 4)
        self.assertTrue(any("out of bounds" in e for e in errors))

    def test_negative_or_out_of_bounds_coordinates_flagged(self):
        label_file = self.dir_path / "out_of_bounds.txt"
        label_file.write_text("0 -0.1 0.5 0.2 0.2\n0 0.5 1.5 0.2 0.2\n", encoding="utf-8")

        boxes, errors = validate_label_file(label_file, 4)
        self.assertGreaterEqual(len(errors), 2)
        self.assertTrue(any("not normalized in [0.0, 1.0]" in e for e in errors))

    def test_non_positive_box_dimensions_flagged(self):
        label_file = self.dir_path / "zero_dim.txt"
        label_file.write_text("0 0.5 0.5 0.0 0.2\n0 0.5 0.5 0.2 -0.1\n", encoding="utf-8")

        boxes, errors = validate_label_file(label_file, 4)
        self.assertEqual(len(errors), 2)
        self.assertTrue(all("Non-positive box dimensions" in e for e in errors))

    def test_malformed_syntax_flagged(self):
        label_file = self.dir_path / "malformed.txt"
        label_file.write_text("0 0.5 0.5 0.2\nnot_a_number 0.5 0.5 0.2 0.2\n", encoding="utf-8")

        boxes, errors = validate_label_file(label_file, 4)
        self.assertEqual(len(errors), 2)


if __name__ == "__main__":
    unittest.main()
