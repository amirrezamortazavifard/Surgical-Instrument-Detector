# Software Architecture: Surgical Instrument Detector

## 1. System Overview

The **Surgical Instrument Detector** is an open-source computer vision research desktop application designed for surgical instrument object detection, clinical instrument catalog reference, and optional AI-assisted consultation.

The repository is structured into two decoupled subsystems:
1. **Model Engineering & Validation Subsystem (`Python/`):** Dataset validation, train/val/test splitting, deep learning training with Ultralytics YOLOv11, test set evaluation, and ONNX graph export.
2. **Desktop Runtime Application (`SurgicalDetector/`):** A native Windows Presentation Foundation (WPF) client running on .NET 8, implementing the Model-View-ViewModel (MVVM) design pattern with dependency injection, local ONNX Runtime model inference, and DirectShow camera capture.

---

## 2. Component & Module Architecture

```mermaid
graph TD
    subgraph UI_Layer ["Presentation Layer (WPF / MVVM)"]
        MW["MainWindow (Shell)"]
        ShellVM["ShellViewModel"]
        ImgVM["ImageDetectionViewModel"]
        LiveVM["LiveDetectionViewModel"]
        DbVM["InstrumentDatabaseViewModel"]
        ChatVM["ChatViewModel"]
        SetVM["SettingsViewModel"]
    end

    subgraph Core_Services ["Service Layer (Inversion of Control)"]
        NavSvc["NavigationService (INavigationService)"]
        ConfigSvc["ConfigurationService (IConfigurationService)"]
        CamSvc["OpenCvCameraService (ICameraService)"]
        DetSvc["OnnxDetectionService (IDetectionService)"]
        InstSvc["InstrumentService (IInstrumentService)"]
        ChatSvc["ChatService (IChatService)"]
        BaymaxSvc["BaymaxAiService (IBaymaxAiService)"]
        BenchSvc["InferenceBenchmarkService"]
    end

    subgraph External_Engines ["Native & External Runtimes"]
        DirectShow["Camera Hardware / DirectShow (OpenCvSharp4)"]
        ONNXEngine["Microsoft.ML.OnnxRuntime (best.onnx)"]
        GeminiAPI["Google Gemini REST API (gemini-2.5-flash)"]
        LocalStore["Local AppData Storage (%LOCALAPPDATA%/SurgicalDetector)"]
    end

    MW --> ShellVM
    ShellVM --> NavSvc
    ShellVM --> ImgVM
    ShellVM --> LiveVM
    ShellVM --> DbVM
    ShellVM --> ChatVM
    ShellVM --> SetVM

    LiveVM --> CamSvc
    LiveVM --> DetSvc
    ImgVM --> DetSvc
    ImgVM --> InstSvc
    ImgVM --> BaymaxSvc
    ChatVM --> ChatSvc
    SetVM --> ConfigSvc
    SetVM --> DetSvc

    CamSvc --> DirectShow
    DetSvc --> ONNXEngine
    BaymaxSvc --> GeminiAPI
    ChatSvc --> GeminiAPI
    ConfigSvc --> LocalStore
```

### Module Responsibilities

| Module / Component | Implementation Location | Responsibility |
| :--- | :--- | :--- |
| **`MainWindow` & `ShellViewModel`** | `SurgicalDetector/MainWindow.xaml`, `ViewModels/ShellViewModel.cs` | Hosts navigation chrome, application window lifecycle, and view switching. |
| **`NavigationService`** | `Services/NavigationService.cs`, `Services/INavigationAware.cs` | Manages view activation and teardown via `INavigationAware` (`OnNavigatedTo`, `OnNavigatedFrom`). |
| **`OnnxDetectionService`** | `Services/OnnxDetectionService.cs`, `Services/IDetectionService.cs` | Manages ONNX session initialization, input letterbox preprocessing, inference execution, coordinate transformation, and class-aware/class-agnostic NMS. |
| **`OpenCvCameraService`** | `Services/OpenCvCameraService.cs`, `Services/ICameraService.cs` | Encapsulates camera enumeration and asynchronous capture using OpenCvSharp DirectShow backend. |
| **`LiveDetectionViewModel`** | `ViewModels/LiveDetectionViewModel.cs` | Manages camera lifecycle, latest-frame non-blocking queue (`Interlocked.Exchange`), background inference dispatch, and UI telemetry updates. |
| **`ImageDetectionViewModel`** | `ViewModels/ImageDetectionViewModel.cs` | Handles static image loading, parameter-driven object detection, bounding-box rendering, and export. |
| **`InstrumentDatabaseViewModel` & `InstrumentService`** | `ViewModels/InstrumentDatabaseViewModel.cs`, `Services/InstrumentService.cs` | Provides local catalog of surgical instruments, handling specifications, sterilization parameters, and clinical indications. |
| **`ConfigurationService`** | `Infrastructure/ConfigurationService.cs` | Multi-tier credential and settings resolution (`GEMINI_API_KEY` env var -> `%LOCALAPPDATA%\SurgicalDetector\config.json`). |
| **`BaymaxAiService` & `ChatService`** | `Services/BaymaxAiService.cs`, `Services/ChatService.cs` | HTTP client wrapper for Google Gemini REST API (`gemini-2.5-flash`) for clinical question-answering with optional image context. |

---

## 3. Dependency Injection & MVVM Design

The application configures its dependency injection container during startup in [`App.xaml.cs`](../SurgicalDetector/SurgicalDetector/App.xaml.cs) using `Microsoft.Extensions.DependencyInjection`:

- **Singleton Services:**
  - `IConfigurationService` (`ConfigurationService`): Thread-safe settings management.
  - `INavigationService` (`NavigationService`): View navigation state.
  - `IDetectionService` (`OnnxDetectionService`): Heavyweight ONNX inference session (`InferenceSession`) initialized once and reused across detection requests.
  - `ICameraService` (`OpenCvCameraService`): Hardware camera resource manager.
  - `IInstrumentService` (`InstrumentService`): In-memory instrument reference database.
  - `IChatService` (`ChatService`) & `IBaymaxAiService` (`BaymaxAiService`): Asynchronous HTTP clients.
- **Singleton ViewModels:**
  - `ShellViewModel`, `LiveDetectionViewModel`, `ImageDetectionViewModel`, `InstrumentDatabaseViewModel`, `ChatViewModel`, `SettingsViewModel`.
  - Registered as singletons to preserve user input and navigation state while using `INavigationAware` to halt hardware-intensive background workers on tab departure.

---

## 4. Separation of Concerns: Training vs. Export vs. Runtime

```
+-----------------------------------------------------------------------------------+
|                        OFFLINE TRAINING & EXPORT (Python)                         |
|                                                                                   |
|  [Raw Dataset] -> train_test_val_split.py -> validate_dataset.py -> train.py     |
|                                                                          |        |
|                                                                     (PyTorch .pt) |
|                                                                          v        |
|  [Independent Test Set] <- run_test_evaluation.py <------------------ export.py   |
|                                                                          |        |
|                                                                    (best.onnx)    |
+--------------------------------------------------------------------------|--------+
                                                                           |
                                                                           v
+-----------------------------------------------------------------------------------+
|                          ONLINE RUNTIME DESKTOP (.NET 8)                          |
|                                                                                   |
|  [Camera / Image File]                                                            |
|          |                                                                        |
|          v                                                                        |
|  [OpenCvSharp4 / DirectShow] -> [Letterbox 640x640]                               |
|                                         |                                         |
|                                         v                                         |
|                              [Microsoft.ML.OnnxRuntime]                           |
|                               (CPUExecutionProvider)                              |
|                                         |                                         |
|                                         v                                         |
|                         [Inverse Transform & Decoded NMS]                         |
|                                         |                                         |
|                                         v                                         |
|                             [WPF UI Viewport Display]                             |
+-----------------------------------------------------------------------------------+
```

1. **Training Subsystem (`Python/`):**
   - Implemented with PyTorch and Ultralytics YOLOv11.
   - Outputs PyTorch checkpoint `Python/YOLO_Result/weights/best.pt`.
   - Validates on independent test partition (`Python/run_test_evaluation.py`).
2. **Export Bridge (`Python/export.py`):**
   - Converts PyTorch checkpoint to ONNX format with static shape `[1, 3, 640, 640]` and Opset 19.
   - Verifies numerical parity between PyTorch outputs and ONNX Runtime outputs.
   - Deploys exported graph to `SurgicalDetector/SurgicalDetector/best.onnx`.
3. **Runtime Subsystem (`SurgicalDetector/`):**
   - Independent of Python; relies entirely on native C# and `Microsoft.ML.OnnxRuntime`.
   - Directly parses raw model output tensor `[1, 8, 8400]`.

---

## 5. Vision Inference Pipeline & Flowchart

```mermaid
flowchart TD
    A["Raw Input Frame (Bitmap / Mat)<br/>e.g., 1920x1080 or 640x480"] --> B["Compute Letterbox Parameters<br/>scale = min(640/W, 640/H)<br/>pad_x = (640 - round(W*scale))/2<br/>pad_y = (640 - round(H*scale))/2"]
    B --> C["Generate Letterboxed Canvas (640x640)<br/>Background Fill: RGB(114, 114, 114)<br/>Bilinear Interpolation Resize"]
    C --> D["Vectorized Normalization & Tensor Copy<br/>Pixel / 255.0f (Float32, RGB Ordering)<br/>Shape: [1, 3, 640, 640]"]
    D --> E["Execute ONNX Inference<br/>InferenceSession.Run()<br/>Input: 'images' -> Output: 'output0'"]
    E --> F["Decode Output Tensor [1, 8, 8400]<br/>Extract: cx, cy, w, h, class_scores[0..3]"]
    F --> G{"Filter by Confidence<br/>MaxScore >= ConfidenceThreshold?"}
    G -- "No" --> H["Discard Prediction"]
    G -- "Yes" --> I["Invert Letterbox Coordinates<br/>x = (cx - w/2 - pad_x) / scale<br/>y = (cy - h/2 - pad_y) / scale<br/>w_orig = w / scale, h_orig = h / scale"]
    I --> J["Clamp to Source Image Bounds"]
    J --> K{"NMS Strategy"}
    K -- "Class-Aware" --> L["Partition Detections by Class<br/>Apply Greedy IoU Suppression per Class"]
    K -- "Class-Agnostic" --> M["Apply Greedy IoU Suppression Globally"]
    L --> N["Sort by Descending Confidence"]
    M --> N
    N --> O["Construct List of DetectionResult<br/>High-Contrast Color, Bounding Box, Label, Score"]
    O --> P["Dispatch to WPF UI Thread via Dispatcher"]
```

---

## 6. Camera Capture & Frame Management Workflows

```mermaid
sequenceDiagram
    autonumber
    participant HW as Camera Hardware (DirectShow)
    participant CS as OpenCvCameraService
    participant LVM as LiveDetectionViewModel
    participant ORT as OnnxDetectionService
    participant UI as WPF Viewport

    Note over CS: Background capture worker active (30 FPS)
    HW->>CS: Capture new video frame
    CS->>LVM: FrameCaptured(Mat frame)
    Note over LVM: Interlocked.Exchange(ref _latestFrame, frame)<br/>If unconsumed frame was pending, dispose it immediately!
    LVM->>ORT: Run detection in background worker (Task.Run)
    Note over ORT: ~800 ms CPU inference on 640x640 tensor
    ORT-->>LVM: List<DetectionResult>
    LVM->>LVM: Render annotations on Mat using cached GDI font
    LVM->>UI: Dispatcher.InvokeAsync(LiveImageSource, FPS, Latency)
    UI-->>UI: Update display
```

---

## 7. Actual Dependencies and Model Artifacts

### Runtime Package Manifest (`SurgicalDetector.csproj`)
- **Target Framework:** `net8.0-windows`
- **`Microsoft.ML.OnnxRuntime`:** Version `1.22.0` (CPU execution package; `CPUExecutionProvider` active).
- **`OpenCvSharp4`:** Version `4.10.0.20241108` (native video capture and matrix structures).
- **`OpenCvSharp4.WpfExtensions`:** Version `4.10.0.20241108` (fast `WriteableBitmap` conversion).
- **`OpenCvSharp4.runtime.win`:** Version `4.10.0.20241108` (packaged Windows native binaries).
- **`Microsoft.Extensions.DependencyInjection`:** Version `9.0.10` (IoC container).
- **`System.Drawing.Common`:** Version `9.0.6` (GDI+ bitmap rendering and coordinate geometry).

### Deployed Model Artifact
- **Path:** [`SurgicalDetector/SurgicalDetector/best.onnx`](../SurgicalDetector/SurgicalDetector/best.onnx)
- **Filesize:** 11,208,495 bytes (~11.2 MB)
- **Model Topology:** Ultralytics YOLOv11 large architecture (`yolov11l`)
- **Input Node:** Name: `images`, Shape: `[1, 3, 640, 640]`, Type: `float32`
- **Output Node:** Name: `output0`, Shape: `[1, 8, 8400]`, Type: `float32`
- **Trained Target Classes (4):**
  - Index `0`: `Scalpel`
  - Index `1`: `Straight Dissection Clamp`
  - Index `2`: `Straight Mayo Scissor`
  - Index `3`: `Curved Mayo Scissor`
