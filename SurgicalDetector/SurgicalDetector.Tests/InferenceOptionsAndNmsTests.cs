using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class InferenceOptionsAndNmsTests
    {
        [Fact]
        public void InferenceOptions_ValidationAndClamping_WorksCorrectly()
        {
            var options = new InferenceOptions();

            options.ConfidenceThreshold = -0.5f;
            Assert.True(options.ConfidenceThreshold >= 0.01f);

            options.ConfidenceThreshold = 1.5f;
            Assert.True(options.ConfidenceThreshold <= 0.99f);

            options.NmsThreshold = 0.0f;
            Assert.True(options.NmsThreshold >= 0.01f);

            options.MaxDetections = 0;
            Assert.Equal(1, options.MaxDetections);

            options.TargetWidth = 10;
            Assert.True(options.TargetWidth >= 32);

            var cloned = options.Clone();
            Assert.Equal(options.ConfidenceThreshold, cloned.ConfidenceThreshold);
            Assert.Equal(options.NmsThreshold, cloned.NmsThreshold);
            Assert.Equal(options.MaxDetections, cloned.MaxDetections);
        }

        [Fact]
        public void ApplyNMS_MaxDetectionsLimit_CapsOutputList()
        {
            var detections = new List<DetectionResult>
            {
                new DetectionResult { Label = "Scalpel", Confidence = 0.95f, Box = new Rectangle(0, 0, 50, 50) },
                new DetectionResult { Label = "Scalpel", Confidence = 0.90f, Box = new Rectangle(100, 100, 50, 50) },
                new DetectionResult { Label = "Scalpel", Confidence = 0.85f, Box = new Rectangle(200, 200, 50, 50) }
            };

            // All 3 are disjoint, but maxDetections is set to 2
            var results = OnnxDetectionService.ApplyNMS(detections, nmsThreshold: 0.45f, classAgnostic: false, maxDetections: 2);

            Assert.Equal(2, results.Count);
            Assert.Equal(0.95f, results[0].Confidence);
            Assert.Equal(0.90f, results[1].Confidence);
        }

        [Fact]
        public void ApplyNMS_EqualConfidenceCandidates_BehavesDeterministically()
        {
            var detections = new List<DetectionResult>
            {
                new DetectionResult { Label = "Scalpel", Confidence = 0.80f, Box = new Rectangle(0, 0, 50, 50) },
                new DetectionResult { Label = "Scalpel", Confidence = 0.80f, Box = new Rectangle(100, 100, 50, 50) }
            };

            var run1 = OnnxDetectionService.ApplyNMS(detections, 0.45f);
            var run2 = OnnxDetectionService.ApplyNMS(detections, 0.45f);

            Assert.Equal(run1.Count, run2.Count);
            Assert.Equal(run1[0].Box, run2[0].Box);
        }
    }
}
