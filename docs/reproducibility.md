# Scientific Reproducibility Guide: Surgical Instrument Detector

## 1. Scientific Standards & Ground Truth Integrity

This document provides exact, deterministic instructions to reproduce the dataset partitions, dataset audit, training runs, evaluation benchmarks, ONNX model export, and desktop runtime execution for the **Surgical Instrument Detector** research project.

### Core Scientific Rules
- **Evidence-Based Reporting:** All evaluation metrics reported in documentation match genuine experimental runs and verified training logs. Unmeasured metrics are explicitly marked as `TO BE MEASURED`.
- **Model Weight Immutability:** Trained weights in `Python/YOLO_Result/weights/best.pt` and exported ONNX graph `SurgicalDetector/SurgicalDetector/best.onnx` reflect verified training states.
- **Independent Test Split:** The test split is held completely separate from training and validation partitions to assess true out-of-sample generalization.
- **Hardware & Numerical Variance:** While seed locking guarantees algorithmic determinism in data shuffling and weight initialization, exact bit-for-bit floating-point reproducibility across different GPU hardware architectures, cuDNN kernel implementations, or non-deterministic atomic CUDA operations is not guaranteed by PyTorch/cuDNN.

---

## 2. Operating System and Runtime Prerequisites

### 2.1 C# / .NET Desktop Application Runtime
- **Operating System:** Windows 10 (1809+) or Windows 11 (64-bit x64). (WPF desktop UI is Windows-specific).
- **Runtime / SDK:** Microsoft .NET 8.0 SDK (Version 8.0.x or 9.0.x targeting `net8.0-windows`).
- **Development Tooling:** Visual Studio 2022 (v17.8+ with ".NET Desktop Development" workload) or .NET CLI with C# Dev Kit.
- **Managed Dependencies (NuGet):**
  - `Microsoft.Extensions.DependencyInjection` (v9.0.10)
  - `Microsoft.ML.OnnxRuntime` (v1.22.0, CPU execution package)
  - `OpenCvSharp4` (v4.10.0.20241108)
  - `OpenCvSharp4.WpfExtensions` (v4.10.0.20241108)
  - `OpenCvSharp4.runtime.win` (v4.10.0.20241108)
  - `System.Drawing.Common` (v9.0.6)
  - `xunit` (v2.8.2) & `xunit.runner.visualstudio` (v2.8.2)

### 2.2 Python Machine Learning & Export Environment
- **Operating System:** Windows 10/11, Ubuntu 20.04+, or macOS 12+ (x64 / arm64).
- **Python Version:** Python 3.10 or 3.11 (64-bit). (Python 3.11.0 verified).
- **Core Dependencies:**
  ```bash
  pip install torch torchvision ultralytics opencv-python onnx onnxruntime pyyaml numpy pillow
  ```
- **Verified Dependency Matrix:**
  - `torch`: 2.11.0
  - `ultralytics`: 8.4.33
  - `onnx`: 1.21.0
  - `onnxruntime`: 1.27.0
  - `opencv-python`: 4.13.0
  - `pyyaml`: 6.0.2
  - `numpy`: 1.26.4

---

## 3. Dataset Attribution & Directory Structure

- **Dataset Title:** [Labeled Surgical Tools and Images](https://www.kaggle.com/datasets/dilavado/labeled-surgical-tools)
- **Author / Curator:** Danilo Lavado (Kaggle)
- **Target Taxonomy (4 Classes):**
  - Index `0`: `Scalpel`
  - Index `1`: `Straight Dissection Clamp`
  - Index `2`: `Straight Mayo Scissor`
  - Index `3`: `Curved Mayo Scissor`

### Dataset Directory Hierarchy
```
Python/
└── datasets/
    └── surg/
        ├── images/
        │   ├── train/     # 1,558 training images (.jpg)
        │   ├── val/       # 276 validation images (.jpg)
        │   └── test/      # 786 independent test images (.jpg)
        └── labels/
            ├── train/     # 1,558 training label annotations (.txt)
            ├── val/       # 276 validation label annotations (.txt)
            └── test/      # 786 test label annotations (.txt)
```

### Partition Distribution (Verified Counts)
- **Train Split:** 1,558 images, 1,558 label files (537 Scalpel, 533 Clamp, 541 Straight Scissor, 535 Curved Scissor).
- **Validation Split:** 276 images, 276 label files (100 Scalpel, 104 Clamp, 96 Straight Scissor, 102 Curved Scissor).
- **Test Split:** 786 images, 786 label files (273 Scalpel, 273 Clamp, 272 Straight Scissor, 274 Curved Scissor).
- **Total Dataset:** 2,620 images, 2,620 labels, 3,640 bounding box instances.
- **Partition Independence:** Exactly 0 shared samples between train, val, and test partitions (mutually disjoint sets).

---

## 4. Step-by-Step Reproduction Workflow

### Step 1: Environment Setup
```bash
# Clone the repository
git clone https://github.com/amirrezamortazavifard/Surgical-Instrument-Detector.git
cd Surgical-Instrument-Detector

# Create and activate Python virtual environment
python -m venv venv
# On Windows:
.\venv\Scripts\activate
# On Linux/macOS:
source venv/bin/activate

# Install required packages
pip install torch torchvision ultralytics opencv-python onnx onnxruntime pyyaml numpy pillow
```

### Step 2: Deterministic Dataset Partitioning
Partition the raw labeled dataset into train, validation, and test splits with a fixed seed (`--seed 42`):
```bash
python Python/train_test_val_split.py \
    --source "source_dataset/labeled surgical tools" \
    --dest "Python/datasets/surg" \
    --val-split 0.15 \
    --seed 42 \
    --report-json "Python/reports/dataset_split_report.json"
```

### Step 3: Dataset Integrity & Leakage Verification
Execute automated validation to ensure label syntax correctness, bounding box coordinate normalization `[0.0, 1.0]`, and split disjointness:
```bash
python Python/validate_dataset.py \
    --data "Python/data.yaml" \
    --output-json "Python/reports/dataset_integrity_report.json" \
    --output-csv "Python/reports/dataset_class_distribution.csv" \
    --strict
```

### Step 4: Model Training (YOLOv11l)
Train YOLOv11 large architecture on the surgical tools dataset:
```bash
python Python/train.py \
    --model "yolo11l.pt" \
    --data "Python/data.yaml" \
    --epochs 100 \
    --batch 16 \
    --imgsz 640 \
    --lr0 0.01 \
    --seed 0 \
    --device 0 \
    --project "Python/runs/train" \
    --name "surgical_yolo11l"
```
*Dry-run option to verify environment and dataset paths without training:*
```bash
python Python/train.py --data "Python/data.yaml" --dry-run
```

### Step 5: Test Partition Benchmark Evaluation
Run benchmark evaluation on the independent test set:
```bash
python Python/run_test_evaluation.py \
    --model "Python/YOLO_Result/weights/best.pt" \
    --data "Python/data.yaml" \
    --split test \
    --conf 0.5 \
    --iou 0.7 \
    --imgsz 640 \
    --output-json "Python/reports/evaluation_report.json" \
    --output-csv "Python/reports/evaluation_metrics.csv"
```

### Step 6: ONNX Model Export & Verification
Export the trained PyTorch checkpoint to ONNX with static shape `[1, 3, 640, 640]`, Opset 19, and graph simplification:
```bash
python Python/export.py \
    --model "Python/YOLO_Result/weights/best.pt" \
    --imgsz 640 \
    --opset 19 \
    --simplify \
    --output "SurgicalDetector/SurgicalDetector/best.onnx" \
    --test-image "Images/Test/test_image.jpg"
```

### Step 7: Desktop Application Launch & Configuration
1. **Restore and Build .NET Solution:**
   ```powershell
   dotnet restore SurgicalDetector/SurgicalDetector.sln
   dotnet build SurgicalDetector/SurgicalDetector.sln -c Release
   ```
2. **Execute Automated Test Suite:**
   ```powershell
   dotnet test SurgicalDetector/SurgicalDetector.sln --logger "console;verbosity=normal"
   ```
3. **Configure Optional Gemini API Key:**
   Set the environment variable or create `%LOCALAPPDATA%\SurgicalDetector\config.json`:
   ```powershell
   $env:GEMINI_API_KEY = "your-api-key-here"
   ```
   Or edit via the in-app **Settings** view. (If unconfigured, detection features remain 100% functional while chat displays a friendly configuration notice).
4. **Launch Application:**
   ```powershell
   dotnet run --project SurgicalDetector/SurgicalDetector/SurgicalDetector.csproj -c Release
   ```

---

## 5. Verified Historical Training Telemetry (EXP-001)

The following metrics are extracted directly from the verified training log `Python/surg/result/results.csv` and `Python/YOLO_Result/results.csv` at epoch 100:
- **Total Training Epochs:** 100
- **Total Training Time:** 7,951.33 seconds (~2.21 hours on single NVIDIA GPU)
- **Batch Size:** 16
- **Final Validation Box Loss:** 0.3124
- **Final Validation Classification Loss:** 0.2496
- **Final Validation DFL Loss:** 0.8851
- **Overall Precision (B):** 0.9892 (98.92%)
- **Overall Recall (B):** 0.9418 (94.18%)
- **mAP@0.50 (B):** 0.9877 (98.77%)
- **mAP@0.50:0.95 (B):** 0.9385 (93.85%)

---

## 6. Known Hardware-Dependent Limitations

1. **CPU vs. GPU Inference Latency:**
   - The desktop client currently executes via `Microsoft.ML.OnnxRuntime` (CPU build).
   - On an 8-core CPU, steady-state throughput is approximately **1.2 FPS** (~800 ms per 640×640 frame).
   - Achieving real-time video streaming (> 30 FPS) requires GPU acceleration (DirectML or CUDA), which requires replacing the NuGet package with `Microsoft.ML.OnnxRuntime.DirectML` or `Microsoft.ML.OnnxRuntime.Gpu` and matching hardware drivers.
2. **DirectShow Video Backend:**
   - The camera capture service leverages the Windows DirectShow multimedia API (`VideoCaptureAPIs.DSHOW`). Camera capture is limited to Windows systems with compatible DirectShow camera drivers.
