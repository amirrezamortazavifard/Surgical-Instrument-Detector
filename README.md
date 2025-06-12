# YOLOv8 Surgical Instrument Detection in C#

![MIT License](https://img.shields.io/badge/License-MIT-green.svg)
![Language C#](https://img.shields.io/badge/Language-C%23-blueviolet)
![Language Python](https://img.shields.io/badge/Language-Python-blue)

This repository contains a complete project for detecting surgical instruments from images. The project is divided into two main parts:
1.  A **Python pipeline** for training a custom YOLOv8 object detection model.
2.  A **C#/.NET Windows Forms application** that uses the trained model to perform real-time inference on user-provided images.

![Detection Demo]([https://i.imgur.com/rLzG9qL.jpg](https://github.com/amirrezamortazavifard/Surgical-Instrument-Detector/blob/main/inference_results/test_image.jpg))


## 📋 Key Features

- **End-to-End Workflow:** Covers the entire process from data preparation and model training to final application deployment.
- **High-Performance Model:** Utilizes the powerful YOLOv8 architecture, fine-tuned on a specific medical dataset.
- **User-Friendly Interface:** A simple and intuitive desktop application built in C# for easy testing and demonstration.
- **Cross-Platform Model:** The project relies on the **ONNX (Open Neural Network Exchange)** format, allowing the Python-trained model to be used seamlessly in the C#/.NET environment.

## 🏗️ Project Architecture

The project is structured in two parts:

1.  **Model Training (Python)**:
    - `organize_dataset.py`: A script to automatically sort the raw dataset into `train`, `val`, and `test` folders.
    - `data.yaml`: The YOLO configuration file defining dataset paths and classes.
    - `train.py`: The main script to train the YOLOv8 model.
    - The output of this pipeline is a trained model, which should be converted to `.onnx` format for use in the C# application.

2.  **Inference Application (C#)**:
    - `Surgical-Instrument-Detector.sln`: The Visual Studio solution file.
    - The C# project loads the `.onnx` model using the `OnnxRuntime` and performs inference on images selected by the user, drawing bounding boxes around detected instruments.

## 🛠️ Technology Stack

- **Inference Application:** C#, .NET Framework, Windows Forms, OnnxRuntime
- **Model Training:** Python, PyTorch, Ultralytics YOLOv8, OpenCV

## 📂 Dataset

This project is trained on the **Labeled Surgical Tools and Images** dataset from Kaggle.
- **Dataset Source:** [Kaggle - Labeled Surgical Tools and Images](https://www.kaggle.com/datasets/dlovado/labeled-surgical-tools)
- **Classes:** `Scalpel`, `Straight Dissection Clamp`, `Straight Mayo Scissor`, `Curved Mayo Scissor`.

## 🚀 Getting Started

You can either run the final C# application or replicate the full training process.

### A. Running the C# Application (Inference)

1.  **Clone the repository:**
    ```sh
    git clone [https://github.com/amirrezamortazavifard/Surgical-Instrument-Detector.git](https://github.com/amirrezamortazavifard/Surgical-Instrument-Detector.git)
    cd Surgical-Instrument-Detector
    ```

2.  **Install Git LFS:** This is required to download the large model files.
    ```sh
    git lfs install
    git lfs pull
    ```

3.  **Open in Visual Studio:** Open the `Surgical-Instrument-Detector.sln` file in Visual Studio.

4.  **Restore Packages:** Restore the necessary NuGet packages (like `OnnxRuntime`).

5.  **Build and Run:** Build the solution and run the application. Use the buttons in the application to load an image and see the detection results.

### B. Replicating the Training Pipeline (Optional)

1.  Follow steps 1-2 from the section above.
2.  Set up a Python environment (e.g., `python -m venv venv`).
3.  Install dependencies: `pip install -r requirements.txt` (You should create this file using `pip freeze > requirements.txt`).
4.  Download the dataset from the Kaggle link and use `organize_dataset.py` to structure it.
5.  Run the training script: `python train.py`.
6.  Convert the best-trained model (`best.pt`) to ONNX format.

## 📈 Results

After training for 100 epochs, the YOLOv8m model achieved the following performance on the validation set.

| Metric     | Value     |
| :--------- | :-------- |
| **mAP50-95** | **`0.XX`** |
| **mAP50** | **`0.XX`** |
| Precision  | `0.XX`    |
| Recall     | `0.XX`    |



## 🙏 Acknowledgments

A special thanks to **Danilo L. V. et al.** for creating and sharing the high-quality "Labeled Surgical Tools and Images" dataset.
