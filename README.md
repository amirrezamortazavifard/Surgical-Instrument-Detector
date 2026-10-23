# Surgical Instrument Detector

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET Version](https://img.shields.io/badge/.NET-8.0--Windows-512BD4.svg?logo=dotnet)](https://dotnet.microsoft.com/)
[![C# Version](https://img.shields.io/badge/C%23-12-239120.svg?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Inference Engine](https://img.shields.io/badge/Inference-ONNX%20Runtime%201.22-005BA1.svg?logo=onnx)](https://onnxruntime.ai/)
[![Model Architecture](https://img.shields.io/badge/Architecture-YOLOv11-00FFFF.svg)](https://github.com/ultralytics/ultralytics)
[![Dataset](https://img.shields.io/badge/Kaggle-Labeled_Surgical_Tools-20BEFF.svg?logo=kaggle)](https://www.kaggle.com/datasets/dilavado/labeled-surgical-tools)
[![Release](https://img.shields.io/github/v/release/amirrezamortazavifard/Surgical-Instrument-Detector?color=success)](https://github.com/amirrezamortazavifard/Surgical-Instrument-Detector/releases/latest)

An enterprise-grade computer vision desktop application for real-time surgical instrument detection and tracking. Built with C# and WPF on .NET 8, the system integrates a custom-trained YOLOv11 deep learning model running directly on Microsoft ONNX Runtime alongside a clinical assistant and an instrument reference database.

---

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Architecture](#architecture)
- [Inference Visualizations](#inference-visualizations)
- [Application Interface](#application-interface)
- [Technology Stack](#technology-stack)
- [Installation and Setup](#installation-and-setup)
- [Model Training Pipeline](#model-training-pipeline)
- [Dataset and Attribution](#dataset-and-attribution)
- [Repository Structure](#repository-structure)
- [Author and Contact](#author-and-contact)
- [License](#license)

---

## Overview

Accurate tracking and verification of surgical instruments is critical for operating room safety, count verification, and surgical workflow analytics. This repository provides an end-to-end implementation spanning:

1. **AI Model Engineering:** Data preparation, training, validation, and export of YOLOv11 object detection weights to the open standard ONNX format.
2. **Medical Desktop Application:** A multi-threaded .NET 8 WPF application built on the MVVM (Model-View-ViewModel) architectural pattern, utilizing DirectShow hardware camera capture, OpenCvSharp4 image processing, and local GPU/CPU hardware acceleration via ONNX Runtime.

---

## Key Features

- **Dual Detection Modalities:**
  - **Live Camera Stream:** Continuous high-framerate inference across connected USB, endoscopic, or built-in camera hardware.
  - **Static Image Analysis:** High-resolution single or batch image processing with detection confidence scoring and bounding box rendering.
- **Hardware Agnostic Acceleration:** Leverages ONNX Runtime execution providers to deliver responsive frame rates without external Python runtime dependencies.
- **Dynamic Parameter Tuning:** Real-time sliders for confidence thresholding and Non-Maximum Suppression (NMS) Intersection-over-Union (IoU) overlap control.
- **Baymax Clinical AI Assistant:** Built-in multimodal medical assistant designed for protocol lookup, surgical instrument discussions, and procedure context.
- **Interactive Instrument Catalog:** Structured database detailing common surgical tools (scalpels, curved and straight Mayo scissors, Metzenbaum scissors, dissection clamps), sterilization parameters, and clinical indications.
- **Operational Telemetry:** Session statistics, instrument frequency distributions, and detection history tracking.

---

## Architecture

The solution separates model development and runtime desktop execution into decoupled sub-systems:

```
Surgical-Instrument-Detector/
├── Python/                        # Training & Validation Subsystem
│   ├── prepare_and_evaluate.py    # Dataset formatting & metric pipelines
│   ├── run_test_evaluation.py     # Batch test evaluation runner
│   ├── data.yaml                  # Class mappings and dataset split definitions
│   └── YOLO_Result/               # Training metrics, PR curves, and weights
│
└── SurgicalDetector/              # Production C# Desktop Application
    └── SurgicalDetector/
        ├── Views/                 # WPF XAML views (Live, Image, Chat, Catalog)
        ├── ViewModels/            # MVVM ViewModels with reactive data binding
        ├── Services/              # ONNX inference, camera capture, telemetry
        ├── Models/                # Detection entities, instrument definitions
        ├── Infrastructure/        # Command delegates, value converters
        └── best.onnx              # Optimized model weights for runtime inference
```

---

## Inference Visualizations

Demonstrating trained model inference on sample surgical scene images from the validation set:

| Sample Input | Model Detection Output |
| :---: | :---: |
| ![Sample Input 1](Images/Test/test_image.jpg) | ![Sample Output 1](Images/Result/test_image.jpg) |
| ![Sample Input 2](Images/Test/test_image1.jpg) | ![Sample Output 2](Images/Result/test_image1.jpg) |
| ![Sample Input 3](Images/Test/test_image2.jpg) | ![Sample Output 3](Images/Result/test_image2.jpg) |

---

## Application Interface

The desktop application user interface is divided into functional modules:

| View / Module | Primary Functionality | Interface Screenshot |
| :--- | :--- | :---: |
| **Live Camera Detection** | Hardware camera feed, real-time bounding box overlay, FPS & tool counter | ![Live Detection](docs/screenshots/live-detection.png) |
| **Static Image Detection** | High-resolution image analysis, adjustable confidence & NMS thresholds | ![Image Detection](docs/screenshots/image-detection.png) |
| **Baymax Clinical Assistant** | Multimodal conversational interface for surgical guidance and query analysis | ![Baymax Assistant](docs/screenshots/baymax-chat.png) |
| **Instrument Catalog** | Database of surgical instruments, handling specifications, and clinical usage | ![Instruments Catalog](docs/screenshots/instruments-catalog.png) |
| **Analytics Dashboard** | Detection telemetry, usage charts, and instrument frequency metrics | ![Analytics](docs/screenshots/analytics-dashboard.png) |

> Note: To customize interface screenshots, save high-resolution PNG images matching the filenames indicated above inside the `docs/screenshots/` directory.

---

## Technology Stack

### Desktop Application (.NET / C#)
- **Framework:** .NET 8.0 (Windows Presentation Foundation)
- **Architecture:** MVVM (Model-View-ViewModel) with Dependency Injection (`Microsoft.Extensions.DependencyInjection`)
- **Inference Runtime:** `Microsoft.ML.OnnxRuntime` 1.22
- **Computer Vision:** `OpenCvSharp4` & `OpenCvSharp4.runtime.win`
- **Design System:** Custom dark-themed surgical monitor styling with high-contrast UI components

### Machine Learning & Training (Python)
- **Deep Learning Framework:** PyTorch & Ultralytics YOLOv11
- **Model Format:** ONNX (Open Neural Network Exchange)
- **Data Processing:** OpenCV-Python, NumPy, Scikit-learn

---

## Installation and Setup

### Option A: Pre-built Binary (Recommended for Users)
1. Navigate to the [Releases](https://github.com/amirrezamortazavifard/Surgical-Instrument-Detector/releases/latest) page.
2. Download the latest standalone archive: `SurgicalDetector-v2.1.0-win-x64.zip`.
3. Extract the contents to a local folder on Windows 10 or 11 (x64).
4. Launch `SurgicalDetector.exe`.

### Option B: Building from Source (Developers)

#### Prerequisites
- Windows 10 or 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with the **.NET Desktop Development** workload installed

#### Build Instructions
1. Clone the repository:
   ```bash
   git clone https://github.com/amirrezamortazavifard/Surgical-Instrument-Detector.git
   cd Surgical-Instrument-Detector
   ```
2. Open the solution file in Visual Studio:
   ```bash
   start SurgicalDetector/SurgicalDetector.sln
   ```
3. Restore dependencies and compile:
   ```powershell
   dotnet restore SurgicalDetector/SurgicalDetector.sln
   dotnet build SurgicalDetector/SurgicalDetector.sln -c Release
   ```
4. Run the application:
   ```powershell
   dotnet run --project SurgicalDetector/SurgicalDetector/SurgicalDetector.csproj -c Release
   ```

---

## Model Training Pipeline

To retrain or fine-tune the model:

1. Configure Python environment:
   ```bash
   cd Python
   python -m venv venv
   .\venv\Scripts\activate
   pip install ultralytics opencv-python pyyaml
   ```
2. Prepare the dataset splits according to `data.yaml`.
3. Execute model training:
   ```bash
   python train.py
   ```
4. Export the resulting PyTorch weights (`best.pt`) to ONNX:
   ```python
   from ultralytics import YOLO
   model = YOLO("runs/detect/train/weights/best.pt")
   model.export(format="onnx", imgsz=640, opset=12)
   ```
5. Place the exported `best.onnx` file into `SurgicalDetector/SurgicalDetector/`.

---

## Dataset and Attribution

The object detection model was trained and evaluated using the **Labeled Surgical Tools and Images** dataset hosted on Kaggle:

[![Kaggle Dataset](https://img.shields.io/badge/Kaggle-Labeled_Surgical_Tools-20BEFF.svg?style=for-the-badge&logo=kaggle&logoColor=white)](https://www.kaggle.com/datasets/dilavado/labeled-surgical-tools)

### Dataset Specifications

| Attribute | Specification |
| :--- | :--- |
| **Dataset Title** | [Labeled Surgical Tools and Images](https://www.kaggle.com/datasets/dilavado/labeled-surgical-tools) |
| **Author / Curator** | **Danilo Lavado** ([@dilavado](https://www.kaggle.com/dilavado)) |
| **Platform** | Kaggle |
| **Sample Size** | 3,009 surgical scene images with high-resolution bounding boxes |
| **Annotation Scope** | Object detection bounding boxes and occlusion labeling |
| **Target Classes** | `Scalpel`, `Straight Dissection Clamp`, `Straight Mayo Scissor`, `Curved Mayo Scissor` |

### Scholarly Citation

We express our gratitude to **Danilo Lavado** for making this labeled surgical dataset openly available for medical computer vision research. If you reference this work or dataset in academic research, please cite the original author:

```bibtex
@misc{dilavado_surgical_tools,
  author       = {Danilo Lavado},
  title        = {Labeled Surgical Tools and Images},
  year         = {2024},
  publisher    = {Kaggle},
  howpublished = {\url{https://www.kaggle.com/datasets/dilavado/labeled-surgical-tools}}
}
```

---

## Repository Structure

| Path | Description |
| :--- | :--- |
| `SurgicalDetector/` | .NET 8 WPF application source code, views, viewmodels, and assets |
| `Python/` | Dataset utilities, training configs, evaluation routines, and model metrics |
| `Images/Test/` | Raw test images for evaluation and manual verification |
| `Images/Result/` | Ground-truth and model inference output visualizations |
| `docs/screenshots/` | High-resolution UI screenshots for documentation |

---

## Author and Contact

**Amirreza Mortazavi Fard**

- **GitHub:** [@amirrezamortazavifard](https://github.com/amirrezamortazavifard)
- **LinkedIn:** [Amirreza Mortazavi Fard](https://www.linkedin.com/in/amir-reza-mortazavi-fard-892874368/)
- **Telegram:** [@ReallyFard](https://t.me/reallyFard)
- **Email:** [mortazavi.a.stu@gmu.ac.ir](mailto:mortazavi.a.stu@gmu.ac.ir)

---

## License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for complete details.
