#!/usr/bin/env python3
"""
test_split_determinism.py
Unit tests verifying reproducibility and determinism of the train/val dataset splitter.
"""

import random
import unittest


def partition_dataset(items, val_fraction, seed):
    """Reference implementation matching train_test_val_split.py logic."""
    sorted_items = sorted(list(items))
    rng = random.Random(seed)
    rng.shuffle(sorted_items)
    split_index = int(len(sorted_items) * (1 - val_fraction))
    val_set = set(sorted_items[split_index:])
    train_set = set(sorted_items[:split_index])
    return train_set, val_set


class TestSplitDeterminism(unittest.TestCase):
    def setUp(self):
        self.sample_files = [f"image_{i:04d}.jpg" for i in range(1000)]

    def test_fixed_seed_produces_identical_partition(self):
        train1, val1 = partition_dataset(self.sample_files, 0.15, seed=42)
        train2, val2 = partition_dataset(self.sample_files, 0.15, seed=42)

        self.assertEqual(train1, train2)
        self.assertEqual(val1, val2)

    def test_different_seeds_produce_distinct_partitions(self):
        train1, val1 = partition_dataset(self.sample_files, 0.15, seed=42)
        train2, val2 = partition_dataset(self.sample_files, 0.15, seed=12345)

        self.assertNotEqual(train1, train2)
        self.assertNotEqual(val1, val2)

    def test_partition_disjointness_and_completeness(self):
        train, val = partition_dataset(self.sample_files, 0.15, seed=42)

        # Check disjointness
        overlap = train & val
        self.assertEqual(len(overlap), 0, f"Leakage detected between train and val: {overlap}")

        # Check completeness
        union = train | val
        self.assertEqual(union, set(self.sample_files))

        # Check exact ratio
        expected_val_count = int(1000 * 0.15)
        self.assertEqual(len(val), expected_val_count)
        self.assertEqual(len(train), 1000 - expected_val_count)


if __name__ == "__main__":
    unittest.main()
