using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SurgicalDetector.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace SurgicalDetector.Services
{
    public class OnnxDetectionService : IDetectionService, IDisposable
    {
        private readonly InferenceSession _session;

        private const string ModelInputName = "images";
        private const string ModelOutputName = "output0";
        private const int TargetWidth = 640;
        private const int TargetHeight = 640;

        private readonly List<string> _classNames = new List<string>
        {
            "Scalpel",
            "Straight Dissection Clamp",
            "Straight Mayo Scissor",
            "Curved Mayo Scissor"
        };

        private const float ConfidenceThreshold = 0.5f;
        private const float NmsThreshold = 0.45f;

        public OnnxDetectionService()
        {
            _session = new InferenceSession("best.onnx");
        }

        public List<DetectionResult> DetectObjects(Bitmap image)
        {
            var preprocessedTensor = PreprocessImage(image);
            using var modelOutput = RunInference(preprocessedTensor);
            var detections = ParseOutput(modelOutput.AsTensor<float>(), image.Size);
            var finalDetections = ApplyNMS(detections);
            return finalDetections;
        }

        private DenseTensor<float> PreprocessImage(Bitmap image)
        {
            using var resizedImage = new Bitmap(TargetWidth, TargetHeight);
            using (var graphics = Graphics.FromImage(resizedImage))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, 0, 0, TargetWidth, TargetHeight);
            }

            var tensor = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });
            for (int y = 0; y < TargetHeight; y++)
            {
                for (int x = 0; x < TargetWidth; x++)
                {
                    var pixel = resizedImage.GetPixel(x, y);
                    tensor[0, 0, y, x] = pixel.R / 255.0f;
                    tensor[0, 1, y, x] = pixel.G / 255.0f;
                    tensor[0, 2, y, x] = pixel.B / 255.0f;
                }
            }
            return tensor;
        }

        private DisposableNamedOnnxValue RunInference(DenseTensor<float> inputTensor)
        {
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(ModelInputName, inputTensor) };
            var results = _session.Run(inputs);
            return results.First(r => r.Name == ModelOutputName);
        }

        private List<DetectionResult> ParseOutput(Tensor<float> output, Size originalSize)
        {
            var results = new List<DetectionResult>();
            int numClasses = _classNames.Count;
            int numDetections = output.Dimensions[2];

            float scaleX = (float)originalSize.Width / TargetWidth;
            float scaleY = (float)originalSize.Height / TargetHeight;

            for (int i = 0; i < numDetections; i++)
            {
                float maxConfidence = 0;
                int classIndex = -1;
                for (int j = 0; j < numClasses; j++)
                {
                    float currentConfidence = output[0, 4 + j, i];
                    if (currentConfidence > maxConfidence)
                    {
                        maxConfidence = currentConfidence;
                        classIndex = j;
                    }
                }

                if (maxConfidence < ConfidenceThreshold) continue;

                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                int x1 = (int)((cx - w / 2) * scaleX);
                int y1 = (int)((cy - h / 2) * scaleY);
                int width = (int)(w * scaleX);
                int height = (int)(h * scaleY);

                results.Add(new DetectionResult
                {
                    Box = new Rectangle(x1, y1, width, height),
                    Confidence = maxConfidence,
                    Label = _classNames[classIndex]
                });
            }
            return results;
        }

        private List<DetectionResult> ApplyNMS(List<DetectionResult> detections)
        {
            var finalDetections = new List<DetectionResult>();
            var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();

            while (sortedDetections.Any())
            {
                var bestDetection = sortedDetections.First();
                finalDetections.Add(bestDetection);
                sortedDetections.Remove(bestDetection);

                for (int i = sortedDetections.Count - 1; i >= 0; i--)
                {
                    if (CalculateIoU(bestDetection.Box, sortedDetections[i].Box) > NmsThreshold)
                    {
                        sortedDetections.RemoveAt(i);
                    }
                }
            }
            return finalDetections;
        }

        private float CalculateIoU(Rectangle boxA, Rectangle boxB)
        {
            int xA = Math.Max(boxA.Left, boxB.Left);
            int yA = Math.Max(boxA.Top, boxB.Top);
            int xB = Math.Min(boxA.Right, boxB.Right);
            int yB = Math.Min(boxA.Bottom, boxB.Bottom);

            float intersectionArea = Math.Max(0, xB - xA + 1) * Math.Max(0, yB - yA + 1);
            if (intersectionArea == 0) return 0;

            float boxAArea = boxA.Width * boxA.Height;
            float boxBArea = boxB.Width * boxB.Height;

            return intersectionArea / (boxAArea + boxBArea - intersectionArea);
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}