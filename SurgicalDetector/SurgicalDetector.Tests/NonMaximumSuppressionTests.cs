using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class NonMaximumSuppressionTests
    {
        [Fact]
        public void ApplyNMS_EmptyInput_ReturnsEmptyList()
        {
            var results = OnnxDetectionService.ApplyNMS(new List<DetectionResult>(), 0.45f);
            Assert.Empty(results);
        }

        [Fact]
        public void ApplyNMS_SameClassOverlappingBoxes_SuppressesLowerConfidence()
        {
            var detections = new List<DetectionResult>
            {
                new DetectionResult
                {
                    Label = "Scalpel",
                    Confidence = 0.92f,
                    Box = new Rectangle(100, 100, 80, 80)
                },
                new DetectionResult
                {
                    Label = "Scalpel",
                    Confidence = 0.75f,
                    Box = new Rectangle(105, 102, 80, 80) // High overlap > 0.8
                }
            };

            var filtered = OnnxDetectionService.ApplyNMS(detections, 0.45f, classAgnostic: false);

            Assert.Single(filtered);
            Assert.Equal(0.92f, filtered[0].Confidence);
            Assert.Equal("Scalpel", filtered[0].Label);
        }

        [Fact]
        public void ApplyNMS_DifferentClassesOverlappingBoxes_PreservesBothInClassAwareMode()
        {
            // A Scalpel and a Curved Mayo Scissor lying right next to or over each other
            var detections = new List<DetectionResult>
            {
                new DetectionResult
                {
                    Label = "Scalpel",
                    Confidence = 0.90f,
                    Box = new Rectangle(100, 100, 100, 100)
                },
                new DetectionResult
                {
                    Label = "Curved Mayo Scissor",
                    Confidence = 0.85f,
                    Box = new Rectangle(100, 100, 100, 100) // Identical box geometry, distinct classes
                }
            };

            var filtered = OnnxDetectionService.ApplyNMS(detections, 0.45f, classAgnostic: false);

            Assert.Equal(2, filtered.Count);
            Assert.Contains(filtered, d => d.Label == "Scalpel");
            Assert.Contains(filtered, d => d.Label == "Curved Mayo Scissor");
        }

        [Fact]
        public void ApplyNMS_DifferentClasses_SuppressesInClassAgnosticMode()
        {
            var detections = new List<DetectionResult>
            {
                new DetectionResult
                {
                    Label = "Scalpel",
                    Confidence = 0.90f,
                    Box = new Rectangle(100, 100, 100, 100)
                },
                new DetectionResult
                {
                    Label = "Curved Mayo Scissor",
                    Confidence = 0.85f,
                    Box = new Rectangle(100, 100, 100, 100)
                }
            };

            var filtered = OnnxDetectionService.ApplyNMS(detections, 0.45f, classAgnostic: true);

            // In class-agnostic mode, the higher confidence box suppresses the other
            Assert.Single(filtered);
            Assert.Equal("Scalpel", filtered[0].Label);
        }
    }
}
