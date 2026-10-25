using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SurgicalDetector.Infrastructure;
using SurgicalDetector.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SurgicalDetector.Services
{
    /// <summary>
    /// Production ONNX Runtime detection service for YOLOv11 surgical instrument models.
    /// Implements exact Ultralytics preprocessing parity, contract validation, and class-aware NMS.
    /// </summary>
    public class OnnxDetectionService : IDetectionService, IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly string _outputName;
        private readonly List<string> _classNames;
        private readonly object _inferenceLock = new object();

        public const string ExpectedInputName = "images";
        public const string ExpectedOutputName = "output0";
        public const int DefaultTargetWidth = 640;
        public const int DefaultTargetHeight = 640;

        private static readonly List<string> DefaultClassNames = new List<string>
        {
            "Scalpel",
            "Straight Dissection Clamp",
            "Straight Mayo Scissor",
            "Curved Mayo Scissor"
        };

        private InferenceOptions? _customOptions;

        public IReadOnlyList<string> ClassNames => _classNames;
        public string ActiveExecutionProvider { get; private set; } = "CPUExecutionProvider";
        public IReadOnlyList<string> AvailableExecutionProviders { get; private set; } = Array.Empty<string>();

        public float ConfidenceThreshold
        {
            get => _customOptions?.ConfidenceThreshold ?? ConfigurationService.Instance.ConfidenceThreshold;
            set
            {
                _customOptions ??= InferenceOptions.Default;
                _customOptions.ConfidenceThreshold = value;
            }
        }

        public float NmsThreshold
        {
            get => _customOptions?.NmsThreshold ?? ConfigurationService.Instance.NmsThreshold;
            set
            {
                _customOptions ??= InferenceOptions.Default;
                _customOptions.NmsThreshold = value;
            }
        }

        public bool UseLetterbox
        {
            get => _customOptions?.UseLetterbox ?? ConfigurationService.Instance.UseLetterbox;
            set
            {
                _customOptions ??= InferenceOptions.Default;
                _customOptions.UseLetterbox = value;
            }
        }

        public OnnxDetectionService() : this("best.onnx")
        {
        }

        public OnnxDetectionService(
            string modelPath,
            InferenceOptions? options = null,
            IEnumerable<string>? customClassNames = null)
        {
            _classNames = customClassNames != null ? new List<string>(customClassNames) : DefaultClassNames;
            _customOptions = options;

            var resolvedPath = ResolveModelPath(modelPath);
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"ONNX detection model artifact not found at: {resolvedPath}", resolvedPath);
            }

            AvailableExecutionProviders = OrtEnv.Instance().GetAvailableProviders();

            var sessionOptions = new SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

            string preferredProvider = options?.ExecutionProvider ?? "CPU";
            ConfigureExecutionProvider(sessionOptions, preferredProvider, options?.DeviceId ?? 0);

            _session = new InferenceSession(resolvedPath, sessionOptions);

            // Validate model input contract
            var inputMeta = _session.InputMetadata;
            if (inputMeta.Count == 0)
            {
                throw new InvalidOperationException("ONNX model contains no input tensors.");
            }

            _inputName = inputMeta.ContainsKey(ExpectedInputName) ? ExpectedInputName : inputMeta.Keys.First();
            var inNode = inputMeta[_inputName];
            if (inNode.Dimensions.Length != 4)
            {
                throw new InvalidOperationException(
                    $"Unsupported input shape for '{_inputName}'. Expected 4 dimensions [batch, channels, height, width], but found {inNode.Dimensions.Length}.");
            }

            // Validate model output contract
            var outputMeta = _session.OutputMetadata;
            if (outputMeta.Count == 0)
            {
                throw new InvalidOperationException("ONNX model contains no output tensors.");
            }

            _outputName = outputMeta.ContainsKey(ExpectedOutputName) ? ExpectedOutputName : outputMeta.Keys.First();
            var outNode = outputMeta[_outputName];
            if (outNode.Dimensions.Length != 3)
            {
                throw new InvalidOperationException(
                    $"Unsupported output shape for '{_outputName}'. Expected 3 dimensions [batch, 4+classes, anchors], but found {outNode.Dimensions.Length}.");
            }

            int expectedFeatures = 4 + _classNames.Count;
            if (outNode.Dimensions[1] > 0 && outNode.Dimensions[1] != expectedFeatures)
            {
                throw new InvalidOperationException(
                    $"Output feature dimension mismatch on '{_outputName}'. Model has {outNode.Dimensions[1]} features, but {expectedFeatures} were expected (4 box coords + {_classNames.Count} classes).");
            }
        }

        private void ConfigureExecutionProvider(SessionOptions sessionOptions, string preferredProvider, int deviceId)
        {
            if (string.Equals(preferredProvider, "CPU", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(preferredProvider, "CPUExecutionProvider", StringComparison.OrdinalIgnoreCase))
            {
                ActiveExecutionProvider = "CPUExecutionProvider";
                return;
            }

            // Check if provider is available in native runtime environment
            bool isAvailable = AvailableExecutionProviders.Any(p => p.IndexOf(preferredProvider, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!isAvailable)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] Requested provider '{preferredProvider}' is not available in current runtime (Available: {string.Join(", ", AvailableExecutionProviders)}). Falling back safely to CPUExecutionProvider.");
                ActiveExecutionProvider = "CPUExecutionProvider";
                return;
            }

            try
            {
                if (preferredProvider.IndexOf("DirectML", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    preferredProvider.IndexOf("Dml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var dmlMethod = typeof(SessionOptions).GetMethod("AppendExecutionProvider_DML", new[] { typeof(int) });
                    if (dmlMethod != null)
                    {
                        dmlMethod.Invoke(sessionOptions, new object[] { deviceId });
                        ActiveExecutionProvider = "DmlExecutionProvider";
                        return;
                    }
                }
                else if (preferredProvider.IndexOf("CUDA", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var cudaMethod = typeof(SessionOptions).GetMethod("AppendExecutionProvider_CUDA", new[] { typeof(int) });
                    if (cudaMethod != null)
                    {
                        cudaMethod.Invoke(sessionOptions, new object[] { deviceId });
                        ActiveExecutionProvider = "CUDAExecutionProvider";
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Could not initialize provider '{preferredProvider}': {ex.Message}. Falling back to CPUExecutionProvider.");
            }

            ActiveExecutionProvider = "CPUExecutionProvider";
        }

        public static string ResolveModelPath(string inputPath)
        {
            if (File.Exists(inputPath)) return Path.GetFullPath(inputPath);

            var inBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, inputPath);
            if (File.Exists(inBaseDir)) return Path.GetFullPath(inBaseDir);

            var inParent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", inputPath);
            if (File.Exists(inParent)) return Path.GetFullPath(inParent);

            var inRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", inputPath);
            if (File.Exists(inRoot)) return Path.GetFullPath(inRoot);

            return inputPath;
        }

        public List<DetectionResult> DetectObjects(Bitmap image)
        {
            var options = _customOptions?.Clone() ?? new InferenceOptions
            {
                ConfidenceThreshold = ConfigurationService.Instance.ConfidenceThreshold,
                NmsThreshold = ConfigurationService.Instance.NmsThreshold,
                UseLetterbox = ConfigurationService.Instance.UseLetterbox,
                TargetWidth = DefaultTargetWidth,
                TargetHeight = DefaultTargetHeight,
                MaxDetections = 300,
                ClassAgnosticNms = false
            };

            return DetectObjects(image, options);
        }

        public List<DetectionResult> DetectObjects(Bitmap image, float confidenceThreshold, float nmsThreshold)
        {
            var options = _customOptions?.Clone() ?? new InferenceOptions();
            options.ConfidenceThreshold = confidenceThreshold;
            options.NmsThreshold = nmsThreshold;
            return DetectObjects(image, options);
        }

        public List<DetectionResult> DetectObjects(Bitmap image, InferenceOptions options)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var tensor = PreprocessImage(
                image,
                options.TargetWidth,
                options.TargetHeight,
                options.UseLetterbox,
                out float scale,
                out float padLeft,
                out float padTop);

            using var modelOutput = RunInference(tensor);
            var parsed = ParseOutput(
                modelOutput.AsTensor<float>(),
                image.Size,
                options,
                scale,
                padLeft,
                padTop,
                _classNames);

            return ApplyNMS(parsed, options.NmsThreshold, options.ClassAgnosticNms, options.MaxDetections);
        }

        private DisposableNamedOnnxValue RunInference(DenseTensor<float> inputTensor)
        {
            lock (_inferenceLock)
            {
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };
                var results = _session.Run(inputs);
                return results.First(r => r.Name == _outputName);
            }
        }

        /// <summary>
        /// Exact Ultralytics letterbox transformation. Preserves aspect ratio, pads with RGB (114, 114, 114),
        /// and converts GDI+ bitmap to normalized [0, 1] RGB tensor.
        /// </summary>
        public static DenseTensor<float> PreprocessImage(
            Bitmap image,
            int targetWidth,
            int targetHeight,
            bool letterbox,
            out float scale,
            out float padLeft,
            out float padTop)
        {
            int origW = image.Width;
            int origH = image.Height;

            int newUnpadW, newUnpadH;
            int left, right, top, bottom;

            if (letterbox)
            {
                scale = Math.Min((float)targetWidth / origW, (float)targetHeight / origH);
                newUnpadW = (int)Math.Round(origW * scale);
                newUnpadH = (int)Math.Round(origH * scale);

                float dw = (targetWidth - newUnpadW) / 2.0f;
                float dh = (targetHeight - newUnpadH) / 2.0f;

                // Ultralytics integer padding rounding: handles odd padding differences exactly
                top = (int)Math.Round(dh - 0.1f);
                bottom = (int)Math.Round(dh + 0.1f);
                left = (int)Math.Round(dw - 0.1f);
                right = (int)Math.Round(dw + 0.1f);

                padLeft = left;
                padTop = top;
            }
            else
            {
                scale = 1.0f;
                newUnpadW = targetWidth;
                newUnpadH = targetHeight;
                left = 0;
                right = 0;
                top = 0;
                bottom = 0;
                padLeft = 0f;
                padTop = 0f;
            }

            using var canvas = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var fillBrush = new SolidBrush(Color.FromArgb(114, 114, 114)))
                {
                    g.FillRectangle(fillBrush, 0, 0, targetWidth, targetHeight);
                }

                g.DrawImage(image, left, top, newUnpadW, newUnpadH);
            }

            var tensor = new DenseTensor<float>(new[] { 1, 3, targetHeight, targetWidth });

            // Direct memory copy ~100x faster than GetPixel
            var rect = new Rectangle(0, 0, targetWidth, targetHeight);
            BitmapData bmpData = canvas.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = bmpData.Stride;
                int bufferSize = Math.Abs(stride) * targetHeight;
                byte[] rawBytes = new byte[bufferSize];
                Marshal.Copy(bmpData.Scan0, rawBytes, 0, bufferSize);

                for (int y = 0; y < targetHeight; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int pixelOffset = rowOffset + (x * 3);
                        // Windows GDI+ 24bpp stores as B, G, R
                        float b = rawBytes[pixelOffset] / 255.0f;
                        float gVal = rawBytes[pixelOffset + 1] / 255.0f;
                        float r = rawBytes[pixelOffset + 2] / 255.0f;

                        tensor[0, 0, y, x] = r;
                        tensor[0, 1, y, x] = gVal;
                        tensor[0, 2, y, x] = b;
                    }
                }
            }
            finally
            {
                canvas.UnlockBits(bmpData);
            }

            return tensor;
        }

        /// <summary>
        /// Parses raw YOLOv11 output tensor [1, 4+numClasses, anchors] into candidate bounding boxes.
        /// Performs confidence filtering, NaN/Inf validation, and inverse coordinate transformation.
        /// </summary>
        public static List<DetectionResult> ParseOutput(
            Tensor<float> output,
            Size originalSize,
            InferenceOptions options,
            float scale,
            float padLeft,
            float padTop,
            IReadOnlyList<string> classNames)
        {
            var results = new List<DetectionResult>();
            int numClasses = classNames.Count;
            int numDetections = output.Dimensions[2]; // e.g. 8400 for 640x640

            int origW = originalSize.Width;
            int origH = originalSize.Height;

            float confThreshold = options.ConfidenceThreshold;
            bool letterbox = options.UseLetterbox;

            float stretchScaleX = letterbox ? 1.0f : (float)origW / options.TargetWidth;
            float stretchScaleY = letterbox ? 1.0f : (float)origH / options.TargetHeight;

            for (int i = 0; i < numDetections; i++)
            {
                float maxConfidence = 0f;
                int classIndex = -1;

                for (int j = 0; j < numClasses; j++)
                {
                    float currentConfidence = output[0, 4 + j, i];
                    if (float.IsNaN(currentConfidence) || float.IsInfinity(currentConfidence)) continue;

                    if (currentConfidence > maxConfidence)
                    {
                        maxConfidence = currentConfidence;
                        classIndex = j;
                    }
                }

                if (maxConfidence < confThreshold || classIndex < 0) continue;

                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                // Guard against non-finite or degenerate candidate boxes
                if (float.IsNaN(cx) || float.IsNaN(cy) || float.IsNaN(w) || float.IsNaN(h) ||
                    float.IsInfinity(cx) || float.IsInfinity(cy) || float.IsInfinity(w) || float.IsInfinity(h) ||
                    w <= 0f || h <= 0f)
                {
                    continue;
                }

                float x1, y1, boxW, boxH;

                if (letterbox)
                {
                    x1 = (cx - (w / 2.0f) - padLeft) / scale;
                    y1 = (cy - (h / 2.0f) - padTop) / scale;
                    boxW = w / scale;
                    boxH = h / scale;
                }
                else
                {
                    x1 = (cx - (w / 2.0f)) * stretchScaleX;
                    y1 = (cy - (h / 2.0f)) * stretchScaleY;
                    boxW = w * stretchScaleX;
                    boxH = h * stretchScaleY;
                }

                // Strict boundary clamping against original image dimensions
                int clampedX = Math.Clamp((int)Math.Round(x1), 0, origW);
                int clampedY = Math.Clamp((int)Math.Round(y1), 0, origH);
                int clampedRight = Math.Clamp((int)Math.Round(x1 + boxW), 0, origW);
                int clampedBottom = Math.Clamp((int)Math.Round(y1 + boxH), 0, origH);

                int finalW = clampedRight - clampedX;
                int finalH = clampedBottom - clampedY;

                if (finalW <= 0 || finalH <= 0) continue;

                results.Add(new DetectionResult
                {
                    Box = new Rectangle(clampedX, clampedY, finalW, finalH),
                    Confidence = maxConfidence,
                    Label = classNames[classIndex]
                });
            }

            return results;
        }

        /// <summary>
        /// Non-Maximum Suppression with support for class-aware (default) and class-agnostic modes.
        /// </summary>
        public static List<DetectionResult> ApplyNMS(
            IEnumerable<DetectionResult> detections,
            float nmsThreshold,
            bool classAgnostic = false,
            int maxDetections = 300)
        {
            if (detections == null) return new List<DetectionResult>();

            var results = new List<DetectionResult>();

            if (classAgnostic)
            {
                var sorted = detections.OrderByDescending(d => d.Confidence).ToList();
                results = PerformNmsSubset(sorted, nmsThreshold);
            }
            else
            {
                var grouped = detections.GroupBy(d => d.Label);
                foreach (var group in grouped)
                {
                    var sorted = group.OrderByDescending(d => d.Confidence).ToList();
                    results.AddRange(PerformNmsSubset(sorted, nmsThreshold));
                }
                results = results.OrderByDescending(d => d.Confidence).ToList();
            }

            if (results.Count > maxDetections)
            {
                results = results.Take(maxDetections).ToList();
            }

            return results;
        }

        private static List<DetectionResult> PerformNmsSubset(List<DetectionResult> sortedDetections, float threshold)
        {
            var results = new List<DetectionResult>();

            while (sortedDetections.Any())
            {
                var best = sortedDetections[0];
                results.Add(best);
                sortedDetections.RemoveAt(0);

                for (int i = sortedDetections.Count - 1; i >= 0; i--)
                {
                    if (CalculateIoU(best.Box, sortedDetections[i].Box) > threshold)
                    {
                        sortedDetections.RemoveAt(i);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Standard Intersection-over-Union (IoU) calculation between two integer pixel bounding boxes.
        /// </summary>
        public static float CalculateIoU(Rectangle boxA, Rectangle boxB)
        {
            int xA = Math.Max(boxA.Left, boxB.Left);
            int yA = Math.Max(boxA.Top, boxB.Top);
            int xB = Math.Min(boxA.Right, boxB.Right);
            int yB = Math.Min(boxA.Bottom, boxB.Bottom);

            int interW = Math.Max(0, xB - xA);
            int interH = Math.Max(0, yB - yA);
            float intersectionArea = (float)interW * interH;

            if (intersectionArea <= 0f) return 0f;

            float boxAArea = (float)boxA.Width * boxA.Height;
            float boxBArea = (float)boxB.Width * boxB.Height;
            float unionArea = boxAArea + boxBArea - intersectionArea;

            return unionArea > 0f ? (intersectionArea / unionArea) : 0f;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}