import os
import shutil
import random
import yaml
from collections import defaultdict
from ultralytics import YOLO

# Paths
DATASET_DIR = r"D:\Sergury\Surgical\Surgical-Dataset"
IMAGES_DIR = os.path.join(DATASET_DIR, "Images", "All", "images")
LABELS_DIR = os.path.join(DATASET_DIR, "Labels", "label object names")
SPLIT_DIR = os.path.join(DATASET_DIR, "Test-Train Groups")

PYTHON_DIR = r"D:\Sergury\Surgical\Surgical-Instrument-Detector\Python"
DATA_YAML = os.path.join(PYTHON_DIR, "data.yaml")
MODEL_PATH = os.path.join(PYTHON_DIR, "YOLO_Result", "weights", "best.pt")

YOLO_DATA_DIR = os.path.join(PYTHON_DIR, "datasets", "surg")

VALIDATION_SPLIT = 0.15

def get_basenames(filepath):
    basenames = []
    with open(filepath, 'r') as f:
        for line in f:
            line = line.strip()
            if line:
                basenames.append(os.path.basename(line))
    return basenames

def main():
    print("Reading splits...")
    train_file = os.path.join(SPLIT_DIR, "train-obj_detector.txt")
    test_file = os.path.join(SPLIT_DIR, "test-obj_detector.txt")
    
    train_basenames = get_basenames(train_file)
    test_basenames = get_basenames(test_file)
    
    random.seed(42)
    random.shuffle(train_basenames)
    split_idx = int(len(train_basenames) * (1 - VALIDATION_SPLIT))
    
    splits = {
        "train": train_basenames[:split_idx],
        "val": train_basenames[split_idx:],
        "test": test_basenames
    }
    
    class_counts = {"train": defaultdict(int), "val": defaultdict(int), "test": defaultdict(int)}
    image_counts = {"train": 0, "val": 0, "test": 0}
    
    for split_name, basenames in splits.items():
        split_img_dir = os.path.join(YOLO_DATA_DIR, "images", split_name)
        split_lbl_dir = os.path.join(YOLO_DATA_DIR, "labels", split_name)
        os.makedirs(split_img_dir, exist_ok=True)
        os.makedirs(split_lbl_dir, exist_ok=True)
        
        for basename in basenames:
            src_img = os.path.join(IMAGES_DIR, basename)
            label_basename = os.path.splitext(basename)[0] + ".txt"
            src_lbl = os.path.join(LABELS_DIR, label_basename)
            
            if os.path.exists(src_img) and os.path.exists(src_lbl):
                dst_img = os.path.join(split_img_dir, basename)
                dst_lbl = os.path.join(split_lbl_dir, label_basename)
                
                shutil.copy2(src_img, dst_img)
                shutil.copy2(src_lbl, dst_lbl)
                
                image_counts[split_name] += 1
                
                with open(src_lbl, 'r') as lf:
                    for line in lf:
                        parts = line.strip().split()
                        if parts:
                            class_counts[split_name][int(parts[0])] += 1
                            
    print("\n--- DATASET STATISTICS ---")
    for split_name in ["train", "val", "test"]:
        print(f"{split_name.upper()} Set:")
        print(f"  Images: {image_counts[split_name]}")
        print(f"  Annotations by class:")
        for cls_id in range(4):
            print(f"    Class {cls_id}: {class_counts[split_name][cls_id]}")
            
    print("\nUpdating data.yaml...")
    with open(DATA_YAML, 'r') as f:
        data_config = yaml.safe_load(f)
        
    data_config['path'] = YOLO_DATA_DIR
    data_config['train'] = 'images/train'
    data_config['val'] = 'images/val'
    data_config['test'] = 'images/test'
    
    with open(DATA_YAML, 'w') as f:
        yaml.safe_dump(data_config, f)
        
    if not os.path.exists(MODEL_PATH):
        print(f"\nError: Model not found at {MODEL_PATH}")
        return
        
    print("\nStarting evaluation on the TEST split...")
    model = YOLO(MODEL_PATH)
    metrics = model.val(data=DATA_YAML, split='test', save_json=False, conf=0.5)
    
    print("\n--- TEST SET RESULTS ---")
    print(f"mAP@0.5:      {metrics.box.map50:.4f}")
    print(f"mAP@0.5:0.95: {metrics.box.map:.4f}")
    
if __name__ == '__main__':
    main()
