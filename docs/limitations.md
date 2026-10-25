# Scientific Limitations & Ethical Considerations

## 1. Scope & Taxonomic Limitations

1. **Restricted Four-Class Taxonomy:**
   The object detection model is trained strictly on four instrument classes:
   - `Scalpel` (Class Index 0)
   - `Straight Dissection Clamp` (Class Index 1)
   - `Straight Mayo Scissor` (Class Index 2)
   - `Curved Mayo Scissor` (Class Index 3)
   Instruments outside this taxonomy (e.g., tissue forceps, DeBakey vascular clamps, Balfour retractors, needle holders, Poole suction tubes) cannot be classified. Unseen instruments risk misclassification into one of the four known classes or omission.

2. **Morphological Ambiguity Between Scissor Variants:**
   `Straight Mayo Scissor` and `Curved Mayo Scissor` differ primarily in distal blade tip curvature. Under perspective foreshortening, head-on camera angles, or partial occlusion, the angular curvature may not be distinguishable, leading to inter-class confusion between scissor types.

3. **Benchtop vs. Intraoperative Visual Domain Gap:**
   The underlying Kaggle training dataset consists primarily of benchtop photographs against clean or draped surfaces. It lacks intraoperative surgical scene complexities, such as:
   - Dynamic blood, tissue, and fluid contamination;
   - High-intensity specular reflections and glare from overhead surgical lighting onto metallic surfaces;
   - Significant hand and glove occlusions;
   - Moving camera jitter and non-perpendicular optical angles.
   Model performance on actual laparoscopic or open-cavity surgical video feeds has not been clinically evaluated.

---

## 2. Validation & Dataset Limitations

1. **Potential Intra-Sequence Correlation (Distribution Shift):**
   In public benchtop datasets, consecutive frames from recorded capture sequences may exhibit near-identical lighting, background textures, and instrument wear. If captured sequences were split randomly rather than grouped by video session, validation metrics (mAP@0.5 = 98.77%) may reflect memorization of benchtop artifacts rather than true out-of-distribution generalizability.

2. **Independent Test Partition:**
   While the dataset split in `Python/datasets/surg` has 0 sample overlap between train (1,558), validation (276), and test (786) sets, all partitions originate from the same underlying dataset collection. Evaluation on external multi-center surgical datasets (e.g., m2cai16-tool, Cholec80, or SAR-RARP50) remains `TO BE MEASURED`.

---

## 3. Inference Preprocessing & Postprocessing Assumptions

1. **Letterbox Aspect-Ratio Preservation:**
   The model expects a square input tensor of `[1, 3, 640, 640]` with letterbox padding color `RGB(114, 114, 114)`. While letterboxing prevents geometric distortion of long instruments (e.g., dissection clamps), significant downsampling of high-resolution camera feeds (e.g., 1080p or 4K to 640×640) can blur fine distal tip features necessary to distinguish subtle blade curvature.

2. **2D Bounding-Box Representation:**
   The system outputs planar 2D bounding boxes $(x, y, w, h)$. It does not estimate:
   - 3D spatial pose or orientation $(R, t)$;
   - Tool tip keypoints or grasp points;
   - Hand-tool interaction states or depth maps.

---

## 4. Hardware, Platform, and Runtime Limitations

1. **CPU Execution Throughput:**
   The application currently links against the standard CPU build of ONNX Runtime (`Microsoft.ML.OnnxRuntime` v1.22.0). 
   - On an 8-core desktop processor (x86_64), end-to-end inference latency is **809.27 ms** per frame, corresponding to a steady-state throughput of **1.2 FPS**.
   - While the desktop client employs a bounded latest-frame strategy (`Interlocked.Exchange`) to prevent UI freezing and memory accumulation, real-time video streaming (> 30 FPS) is unattainable without dedicated GPU hardware acceleration (e.g., via DirectML or CUDA).

2. **Windows DirectShow Dependency:**
   Camera acquisition via `OpenCvSharp4` relies on the Windows DirectShow multimedia API (`VideoCaptureAPIs.DSHOW`). macOS (`AVFoundation`) and Linux (`V4L2`) camera pipelines are not supported without native platform adapters.

3. **GDI+ Dependency:**
   Desktop image annotation relies on `System.Drawing.Common`, which is officially supported only on Windows operating systems.

---

## 5. External API Integration (Baymax Clinical Assistant)

1. **Stateless REST Model (Not an Autonomous Agent):**
   The "Baymax" assistant in `BaymaxAiService` and `ChatService` is a stateless HTTP wrapper communicating with Google's `gemini-2.5-flash` endpoint. It does not possess autonomous agency, external tool execution capabilities, or hospital database integration.
2. **Cloud Connectivity Requirement:**
   The assistant requires active outbound Internet access and a valid `GEMINI_API_KEY`. In network-isolated operating room environments, generative assistant features are unavailable, although local ONNX vision detection operates entirely offline.
3. **No Clinical Certification:**
   The generative assistant is provided strictly for educational reference and research demonstration. It has not undergone FDA, CE-MDR, or institutional clinical review.

---

## 6. Missing Evidence That Prevents Stronger Claims

| Strong Claim | Status | Required Empirical Evidence (Currently Missing) |
| :--- | :--- | :--- |
| **"Clinical / OR Ready"** | **Unsubstantiated** | Multi-center clinical trials, IRB approval, validation on active surgical video with biofluid occlusion and variable lighting. |
| **"Real-Time Video Detection" (on default CPU)** | **Refuted** | CPU execution yields ~1.2 FPS. Real-time claims require GPU execution provider benchmarks (> 30 FPS) with hardware telemetry. |
| **"Robust Generalization Across Hospitals"** | **Unverified** | Testing on external surgical datasets with different surgical tray setups, draping, and lighting conditions. |
| **"Autonomous Surgical Monitoring"** | **Refuted** | The software is an interactive desktop GUI and does not interface with surgical robotic or count-tracking hospital systems. |
