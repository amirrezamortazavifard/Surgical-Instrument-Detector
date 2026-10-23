# Surgical Tool Detector 🩺🤖

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blueviolet.svg)](https://dotnet.microsoft.com/)
[![C# Version](https://img.shields.io/badge/C%23-12-blue.svg)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Python Version](https://img.shields.io/badge/Python-3.9+-brightgreen.svg)](https://www.python.org/)
[![Status](https://img.shields.io/badge/Status-Completed-success.svg)](https://github.com/am-mortazavifard/Surgical-Detector)

An intelligent desktop application for the **real-time detection** of surgical instruments using a **YOLOv11** AI model and a modern user interface built with **C# (WPF)**.

This project was developed with the goal of increasing accuracy and speed in surgical environments, and also serves as a powerful educational example of integrating a Python-based AI workflow with high-performance .NET applications.


---

## ✨ Core Features

* **Dual Detection Modes:**
    * **Image-Based Detection:** Analyze static image files (`.jpg`, `.png`, etc.).
    * **Live Camera Detection:** Process a real-time video feed from any connected webcam.
* **Multi-Camera Support:** Automatically detects all connected cameras and allows the user to choose their desired device.
* **Powerful AI Model:** Utilizes a custom-trained **YOLOv11** model, exported to the **ONNX** format for maximum performance and compatibility.
* **Modern & Responsive UI:**
    * Built with **WPF**, featuring a dark theme inspired by surgical environments.
    * AI processing runs on a background thread to prevent UI freezing and ensure a smooth, responsive experience.
* **Graphical Result Visualization:** Renders bounding boxes, class labels, and confidence scores clearly and colorfully on the image or live video feed.

---

## 🏗️ Project Architecture

This project is divided into two main, independent parts:

1.  **Part 1: AI Model Training (in the `python/` folder)**
    * This section contains all the Python scripts for preparing the dataset, training the **YOLOv11** model, and finally exporting the trained model to the `.onnx` format.
    * This entire workflow is built using Python and the PyTorch framework.

2.  **Part 2: Desktop Application (in the `SurgicalDetector/` folder)**
    * The core application and its user interface, written in **C#** with the **WPF** framework.
    * C# was chosen for this part due to its **superior performance for building responsive desktop applications** compared to Python.
    * The application loads and executes the `.onnx` model generated in the first part to perform inference.

```
surgical-detector/
├── 📂 python/              # Part 1: AI Model Training
│   ├── train.py
│   ├── data.yaml
│   ├── test.py
│   └── ...
└── 📂 SurgicalDetector/     # Part 2: C# Desktop Application
    ├── SurgicalDetector.sln
    ├── SurgicalDetector/
    └── ...
```

---

## 🛠️ Technology Stack

* **AI & Model Training (Python):**
    * ![Python](https://img.shields.io/badge/Python-3.9-blue.svg?logo=python)
    * ![PyTorch](https://img.shields.io/badge/PyTorch-2.0-ee4c2c.svg?logo=pytorch)
    * **YOLOv11**
    * OpenCV-Python

* **Desktop Application (C#):**
    * ![CSharp](https://img.shields.io/badge/C%23-12-purple.svg?logo=c-sharp)
    * **.NET 8**
    * **Windows Presentation Foundation (WPF)**
    * **Microsoft.ML.OnnxRuntime:** For running ONNX models.
    * **OpenCvSharp4:** For image processing and camera interoperability.

---

## 🚀 Setup and Installation

To run this project, you will need the following prerequisites:
* **.NET 8.0 SDK**
* **Python 3.9** or higher
* **Visual Studio 2022** with the ".NET desktop development" workload installed.

1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/amirrezamortazavifard/surgical-detector.git](https://github.com/amirrezamortazavifard/surgical-detector.git)
    cd surgical-detector
    ```

2.  **Set up the Python environment (Optional - for re-training the model):**
    ```bash
    cd python
    pip install -r requirements.txt
    # To train the model, run the training script
    python train.py
    ```

3.  **Set up the C# Application (Main Program):**
    * Open the `SurgicalDetector/` folder and double-click `SurgicalDetector.sln` to open it in Visual Studio 2022.
    * Visual Studio will automatically restore the required NuGet packages.
    * **Rebuild the solution** (from the menu: Build > Rebuild Solution).
    * Press **F5** or the Start button to run the application.

---

## 🧠 AI Model Details

* **Base Model:** The project uses a **YOLOv11** object detection model.
* **Dataset:** The images used for training were sourced from **Kaggle**.
* **Final Format:** After training, the model was converted to the **ONNX (Open Neural Network Exchange)** format. This allows the model to be run on various platforms, including in a C# environment, with high performance and without a dependency on Python.

---

## 🗂️ Repository Contents

This is a complete and comprehensive repository. By cloning it, you will have access to all of the following:
* All **C# and Python source code.**
* The project's **binary and executable files.**
* The final, ready-to-use **`best.onnx` model.**
* **Model training results and logs** (e.g., loss and accuracy charts).

---

## 👤 Creator

**Amirreza Mortazavi Fard (Amir Fard)**

* **Telegram:** [@ReallyFard](https://t.me/reallyFard)
* **GitHub:** [@amirrezamortazavifard](https://github.com/amirrezamortazavifard)
* **LinkedIn:** [Amirreza Mortazavi Fard](https://www.linkedin.com/in/amir-reza-mortazavi-fard-892874368/)
* **Email:** [mortazavi.a.stu@gmu.ac.ir](mailto:mortazavi.a.stu@gmu.ac.ir)

---

## 📜 License

This project is licensed under the **MIT License**. See the `LICENSE` file for details.
