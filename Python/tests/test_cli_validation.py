#!/usr/bin/env python3
"""
test_cli_validation.py
Unit tests verifying CLI argument validation and graceful non-zero error exits on invalid inputs.
"""

import sys
import unittest
import subprocess
from pathlib import Path


class TestCliValidation(unittest.TestCase):
    def setUp(self):
        self.python_dir = Path(__file__).resolve().parent.parent
        self.py_executable = sys.executable

    def test_validate_dataset_missing_file_fails_with_nonzero_code(self):
        cmd = [self.py_executable, str(self.python_dir / "validate_dataset.py"), "--data", "non_existent_data.yaml"]
        result = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("not found", result.stdout + result.stderr)

    def test_train_dry_run_missing_yaml_fails(self):
        cmd = [self.py_executable, str(self.python_dir / "train.py"), "--data", "non_existent_data.yaml", "--dry-run"]
        result = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("FATAL ERROR", result.stdout + result.stderr)

    def test_run_test_evaluation_missing_model_fails(self):
        cmd = [self.py_executable, str(self.python_dir / "run_test_evaluation.py"), "--model", "non_existent_model.pt"]
        result = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("not found", result.stdout + result.stderr)

    def test_export_missing_model_fails(self):
        cmd = [self.py_executable, str(self.python_dir / "export.py"), "--model", "non_existent_model.pt"]
        result = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("not found", result.stdout + result.stderr)

    def test_all_scripts_respond_to_help(self):
        scripts = ["validate_dataset.py", "train_test_val_split.py", "train.py", "run_test_evaluation.py", "export.py"]
        for s in scripts:
            script_path = self.python_dir / s
            cmd = [self.py_executable, str(script_path), "--help"]
            result = subprocess.run(cmd, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, f"Script {s} --help failed: {result.stderr}")
            self.assertIn("usage:", result.stdout.lower(), f"Script {s} didn't display usage: {result.stdout}")


if __name__ == "__main__":
    unittest.main()
