# Research Methods Draft: Surgical Instrument Detector

*This document provides a publication-ready draft of the **Methods** section for a scientific software manuscript. Verified source code facts and experimental observations are strictly separated from unmeasured metrics, which are explicitly marked with `[TO BE MEASURED]` placeholders.*

---

## 1. System Architecture and Design

The software system is engineered as a decoupled, two-tier architecture comprising an offline machine-learning engineering pipeline in Python and an online, native desktop client in C# / .NET 8 (Windows Presentation Foundation).

### 1.1 Inversion of Control & MVVM Presentation
The desktop client follows the Model-View-ViewModel (MVVM) design pattern, utilizing `Microsoft.Extensions.DependencyInjection` to decouple UI presentation from business logic and hardware acquisition:
- High-level view navigation is managed by an inversion-of-control navigation service (`NavigationService`) implementing the `INavigationAware` interface to ensure that background hardware capture and inference threads terminate deterministically upon view transition.
- Hardware camera capture is encapsulated within `OpenCvCameraService`, abstracting the native Windows DirectShow API (`VideoCaptureAPIs.DSHOW`).
- Deep neural network inference is abstracted behind `IDetectionService` and realized via `OnnxDetectionService`, maintaining an isolated, thread-safe `InferenceSession` lifecycle.
- Auxiliary clinical information is served locally by `InstrumentService` and supplemented by an optional cloud-based language model wrapper (`BaymaxAiService` / `ChatService`) communicating with Google's Gemini REST API (`gemini-2.5-flash`).

---

## 2. Dataset Partitioning and Validation Protocol

### 2.1 Dataset Provenance and Class Taxonomy
The system targets object detection across four common surgical instrument classes derived from the open-access *Labeled Surgical Tools and Images* dataset curated by Lavado:
1. `Scalpel` (Class Index 0)
2. `Straight Dissection Clamp` (Class Index 1)
3. `Straight Mayo Scissor` (Class Index 2)
4. `Curved Mayo Scissor` (Class Index 3)

The dataset comprises 2,620 labeled images containing 3,640 annotated bounding box instances.

### 2.2 Deterministic Partitioning and Leakage Prevention
To guarantee scientific reproducibility and prevent data leakage:
- The raw dataset was partitioned into train, validation, and test subsets using a fixed pseudo-random number generator seed (`seed = 42`).
- The partition resulted in:
  - **Training Subset:** 1,558 images (59.5%), 1,558 label files (537 Scalpel, 533 Clamp, 541 Straight Scissor, 535 Curved Scissor).
  - **Validation Subset:** 276 images (10.5%), 276 label files (100 Scalpel, 104 Clamp, 96 Straight Scissor, 102 Curved Scissor).
  - **Test Subset:** 786 images (30.0%), 786 label files (273 Scalpel, 273 Clamp, 272 Straight Scissor, 274 Curved Scissor).
- Automated partition auditing verified zero image or label overlap ($\text{Train} \cap \text{Val} \cap \text{Test} = \emptyset$).
- Bounding box annotations were validated for normalized coordinate boundaries ($x_{\text{center}}, y_{\text{center}}, w, h \in [0.0, 1.0]$) and valid class indices ($c \in \{0, 1, 2, 3\}$).

---

## 3. Deep Learning Model Training and Optimization

### 3.1 Model Architecture
Object detection is performed by the Ultralytics YOLOv11 large architecture (`yolov11l`), incorporating a convolutional cross-stage partial backbone, an enhanced feature pyramid network path aggregation neck, and a decoupled anchor-free detection head.

### 3.2 Training Hyperparameters and Hardware Runtime
Training was conducted over 100 epochs using the Ultralytics deep learning framework on PyTorch 2.11:
- **Input Resolution:** $640 \times 640$ pixels
- **Batch Size:** 16
- **Initial Learning Rate:** $\eta_0 = 0.01$
- **Optimizer:** AdamW / SGD auto-selection
- **Loss Functions:** Complete Intersection over Union (CIoU) bounding-box regression loss, Distribution Focal Loss (DFL), and binary cross-entropy classification loss.
- **Total Training Duration:** 7,951.33 seconds (~2.21 hours) on a single workstation GPU.

### 3.3 Historical Training Outcomes (Verified at Epoch 100)
- **Validation Box Loss:** 0.3124
- **Validation Classification Loss:** 0.2496
- **Validation DFL Loss:** 0.8851
- **Overall Precision:** 0.9892
- **Overall Recall:** 0.9418
- **mAP@0.50:** 0.9877
- **mAP@0.50:0.95:** 0.9385
- **Per-Class Metrics on Independent Test Split:** `[TO BE MEASURED]` *(Evaluation across external hospital datasets: `[TO BE MEASURED]`)*

---

## 4. Model Export and Native Inference Pipeline

### 4.1 ONNX Graph Export
Trained PyTorch weights (`best.pt`) were exported to an Open Neural Network Exchange (ONNX) computation graph (`best.onnx`) with:
- Target Opset: 19
- Static input tensor dimensions: $[1, 3, 640, 640]$ (Batch, Channels, Height, Width)
- Output tensor dimensions: $[1, 8, 8400]$ representing 8,400 candidate anchor predictions with 4 coordinate parameters ($c_x, c_y, w, h$) and 4 class conditional probabilities ($s_0, s_1, s_2, s_3$).
- Graph simplification via `onnxslim` / ONNX graph transform.

### 4.2 Letterbox Geometric Preprocessing
To eliminate aspect-ratio distortion when handling non-square camera frames (e.g., $1920 \times 1080$ or $640 \times 480$):
1. A uniform scaling factor is computed:
   $$r = \min\left(\frac{640}{W_{\text{orig}}}, \frac{640}{H_{\text{orig}}}\right)$$
2. Symmetric letterbox offsets are determined:
   $$\text{pad}_x = \frac{640 - \text{round}(W_{\text{orig}} \cdot r)}{2}, \quad \text{pad}_y = \frac{640 - \text{round}(H_{\text{orig}} \cdot r)}{2}$$
3. The image is bilinearly resized onto a $640 \times 640$ canvas filled with standard neutral padding ($\text{RGB} = 114, 114, 114$).
4. Color channels are normalized from $[0, 255]$ uint8 to $[0.0, 1.0]$ float32 using direct memory-locked pointer access, structured in planar NCHW format.

### 4.3 Coordinate Inversion and Non-Maximum Suppression
Following inference:
1. Predictions with maximum class probability below the user-configured confidence threshold ($\tau_{\text{conf}}$, default 0.50) are discarded.
2. Bounding-box coordinates are mapped back to the original image coordinate frame:
   $$x_1 = \frac{c_x - \frac{w}{2} - \text{pad}_x}{r}, \quad y_1 = \frac{c_y - \frac{h}{2} - \text{pad}_y}{r}$$
   $$w_{\text{orig}} = \frac{w}{r}, \quad h_{\text{orig}} = \frac{h}{r}$$
3. Coordinates are clamped to $[0, W_{\text{orig}}]$ and $[0, H_{\text{orig}}]$.
4. Candidate boxes are partitioned by class index $c \in \{0, 1, 2, 3\}$, and greedy Non-Maximum Suppression is executed independently per class at intersection-over-union threshold $\tau_{\text{IoU}}$ (default 0.45) to prevent inter-class cross-suppression.

---

## 5. Runtime Concurrency and Resource Management

### 5.1 Bounded Latest-Frame Acquisition
When capturing from a 30 FPS camera feed while executing CPU inference (~800 ms latency):
- Frames arriving while the detection worker is busy are handled via an atomic pointer exchange:
  $$\text{stale} = \text{Interlocked.Exchange}(\& \text{latestFrame}, \text{incomingFrame})$$
- Any unconsumed frame referenced by $\text{stale}$ is immediately freed via native `Mat.Dispose()`.
- This bounded hand-off ensures that queue depth is strictly capped at 1, eliminating latency accumulation and memory leakage.

### 5.2 Thread Isolation and Dispatching
- Camera frame acquisition executes on an independent background thread.
- ONNX tensor evaluation executes asynchronously via `Task.Run` on the .NET ThreadPool.
- UI telemetry and bounding-box rendering updates are marshalled onto the WPF Dispatcher thread via asynchronous invocation (`Dispatcher.InvokeAsync`).

---

## 6. Empirical Benchmarking Methodology

### 6.1 Measurement Protocol
Benchmarking was executed using monotonic high-resolution timers (`System.Diagnostics.Stopwatch` in .NET; `time.perf_counter` in Python) under steady-state operating conditions:
- **Warm-up Phase:** 5 unmeasured warm-up iterations to prime JIT compilation and tensor allocation.
- **Measurement Phase:** 20 consecutive inference iterations over representative input data ($640 \times 480$ surgical image).
- **Recorded Intervals:** Model initialization time, cold-start latency, preprocessing duration, raw ONNX session execution duration, postprocessing/NMS duration, and total end-to-end latency.

### 6.2 Hardware and Environment Baseline
- **Operating System:** Windows 10/11 (x64)
- **CPU Architecture:** AMD64 (8 logical processors)
- **Runtime:** .NET 8.0 / Python 3.11.0 (ONNX Runtime 1.22 / 1.27)
- **Active Execution Provider:** `CPUExecutionProvider`

### 6.3 Measured Baseline Performance
- **Model Initialization Latency:** 733.75 ms (Python) / ~820 ms (.NET)
- **Cold-Start Latency:** 821.57 ms (Python) / ~860 ms (.NET)
- **Mean Preprocessing Latency:** 8.76 ms
- **Mean ONNX Inference Latency:** 799.74 ms
- **Mean Postprocessing / NMS Latency:** 0.78 ms
- **Mean End-to-End Latency:** 809.27 ms
- **Steady-State Throughput (CPU):** 1.2 FPS
- **Steady-State Throughput (GPU DirectML / CUDA):** `[TO BE MEASURED]`
