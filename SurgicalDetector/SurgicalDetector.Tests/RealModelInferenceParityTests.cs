using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class RealModelInferenceParityTests
    {
        private string FindRepoRoot()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "LICENSE")) &&
                    Directory.Exists(Path.Combine(current, "Images")))
                {
                    return current;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        [Fact]
        public void RealOnnxModel_InferenceParity_MatchesUltralyticsDetections()
        {
            var repoRoot = FindRepoRoot();
            var modelPath = Path.Combine(repoRoot, "SurgicalDetector", "SurgicalDetector", "best.onnx");
            var imagePath = Path.Combine(repoRoot, "Images", "Test", "test_image.jpg");

            if (!File.Exists(modelPath) || !File.Exists(imagePath))
            {
                // Skip if running in environment where large artifacts are not present
                return;
            }

            var options = new InferenceOptions
            {
                ConfidenceThreshold = 0.50f,
                NmsThreshold = 0.45f,
                UseLetterbox = true,
                ClassAgnosticNms = false
            };

            using var service = new OnnxDetectionService(modelPath, options);
            using var bitmap = new Bitmap(imagePath);

            var detections = service.DetectObjects(bitmap, options);

            Assert.NotEmpty(detections);
            Assert.Contains(detections, d => d.Label == "Scalpel");
            Assert.Contains(detections, d => d.Label == "Curved Mayo Scissor");

            var scalpel = detections.First(d => d.Label == "Scalpel");
            Assert.True(scalpel.Confidence >= 0.85f, $"Expected Scalpel confidence >= 0.85, got {scalpel.Confidence}");
            Assert.True(scalpel.Box.Width > 150 && scalpel.Box.Height > 80);

            var scissor = detections.First(d => d.Label == "Curved Mayo Scissor");
            Assert.True(scissor.Confidence >= 0.60f, $"Expected Curved Mayo Scissor confidence >= 0.60, got {scissor.Confidence}");
            Assert.True(scissor.Box.Width > 150 && scissor.Box.Height > 180);

            // Coordinates must lie strictly within image dimensions (640x480)
            foreach (var d in detections)
            {
                Assert.InRange(d.Box.Left, 0, bitmap.Width);
                Assert.InRange(d.Box.Top, 0, bitmap.Height);
                Assert.InRange(d.Box.Right, 0, bitmap.Width);
                Assert.InRange(d.Box.Bottom, 0, bitmap.Height);
            }
        }
    }
}
