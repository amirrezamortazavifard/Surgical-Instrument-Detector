using Microsoft.ML.OnnxRuntime.Tensors;
using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class OutputParsingAndValidationTests
    {
        private readonly List<string> _classNames = new List<string>
        {
            "Scalpel",
            "Straight Dissection Clamp",
            "Straight Mayo Scissor",
            "Curved Mayo Scissor"
        };

        [Fact]
        public void ParseOutput_ValidCandidates_CorrectlyMappedToCoordinatesAndClasses()
        {
            // Tensor shape [1, 8, 3] with 3 candidates
            var tensor = new DenseTensor<float>(new[] { 1, 8, 3 });

            // Candidate 0: Scalpel (class 0) at center (100, 100), size (40, 60), conf 0.85
            tensor[0, 0, 0] = 100f; // cx
            tensor[0, 1, 0] = 100f; // cy
            tensor[0, 2, 0] = 40f;  // w
            tensor[0, 3, 0] = 60f;  // h
            tensor[0, 4, 0] = 0.85f; // class 0 (Scalpel)
            tensor[0, 5, 0] = 0.10f;
            tensor[0, 6, 0] = 0.05f;
            tensor[0, 7, 0] = 0.00f;

            // Candidate 1: Below confidence threshold (conf 0.30)
            tensor[0, 0, 1] = 200f;
            tensor[0, 1, 1] = 200f;
            tensor[0, 2, 1] = 50f;
            tensor[0, 3, 1] = 50f;
            tensor[0, 4, 1] = 0.10f;
            tensor[0, 5, 1] = 0.30f; // class 1
            tensor[0, 6, 1] = 0.05f;
            tensor[0, 7, 1] = 0.00f;

            // Candidate 2: Curved Mayo Scissor (class 3) at center (300, 300), size (80, 80), conf 0.90
            tensor[0, 0, 2] = 300f;
            tensor[0, 1, 2] = 300f;
            tensor[0, 2, 2] = 80f;
            tensor[0, 3, 2] = 80f;
            tensor[0, 4, 2] = 0.02f;
            tensor[0, 5, 2] = 0.05f;
            tensor[0, 6, 2] = 0.03f;
            tensor[0, 7, 2] = 0.90f; // class 3 (Curved Mayo Scissor)

            var options = new InferenceOptions
            {
                ConfidenceThreshold = 0.50f,
                UseLetterbox = false
            };

            var originalSize = new Size(640, 640);
            var results = OnnxDetectionService.ParseOutput(
                tensor,
                originalSize,
                options,
                scale: 1.0f,
                padLeft: 0f,
                padTop: 0f,
                _classNames);

            Assert.Equal(2, results.Count);

            // Candidate 0: x1 = 100 - 40/2 = 80, y1 = 100 - 60/2 = 70
            Assert.Equal("Scalpel", results[0].Label);
            Assert.Equal(0.85f, results[0].Confidence, 3);
            Assert.Equal(new Rectangle(80, 70, 40, 60), results[0].Box);

            // Candidate 2: x1 = 300 - 80/2 = 260, y1 = 300 - 80/2 = 260
            Assert.Equal("Curved Mayo Scissor", results[1].Label);
            Assert.Equal(0.90f, results[1].Confidence, 3);
            Assert.Equal(new Rectangle(260, 260, 80, 80), results[1].Box);
        }

        [Fact]
        public void ParseOutput_NaNAndInfinityValues_AreSafelyIgnored()
        {
            var tensor = new DenseTensor<float>(new[] { 1, 8, 3 });

            // Candidate with NaN confidence
            tensor[0, 0, 0] = 100f;
            tensor[0, 1, 0] = 100f;
            tensor[0, 2, 0] = 50f;
            tensor[0, 3, 0] = 50f;
            tensor[0, 4, 0] = float.NaN;

            // Candidate with Infinity coordinate
            tensor[0, 0, 1] = float.PositiveInfinity;
            tensor[0, 1, 1] = 100f;
            tensor[0, 2, 1] = 50f;
            tensor[0, 3, 1] = 50f;
            tensor[0, 4, 1] = 0.95f;

            // Candidate with negative dimension
            tensor[0, 0, 2] = 100f;
            tensor[0, 1, 2] = 100f;
            tensor[0, 2, 2] = -10f;
            tensor[0, 3, 2] = 50f;
            tensor[0, 4, 2] = 0.95f;

            var options = new InferenceOptions { ConfidenceThreshold = 0.5f };
            var results = OnnxDetectionService.ParseOutput(
                tensor,
                new Size(640, 640),
                options,
                1.0f, 0f, 0f,
                _classNames);

            Assert.Empty(results);
        }

        [Fact]
        public void ParseOutput_EdgePositionedBox_ClampedToImageBoundary()
        {
            // Box positioned with center at (10, 10) and size (60, 60) on 640x640
            // Theoretical x1 = 10 - 30 = -20, y1 = 10 - 30 = -20
            // Clamped: x1 = 0, y1 = 0, right = 40, bottom = 40 -> w=40, h=40
            var tensor = new DenseTensor<float>(new[] { 1, 8, 1 });
            tensor[0, 0, 0] = 10f;
            tensor[0, 1, 0] = 10f;
            tensor[0, 2, 0] = 60f;
            tensor[0, 3, 0] = 60f;
            tensor[0, 4, 0] = 0.90f; // Scalpel

            var options = new InferenceOptions { ConfidenceThreshold = 0.5f, UseLetterbox = false };
            var results = OnnxDetectionService.ParseOutput(
                tensor,
                new Size(640, 640),
                options,
                1.0f, 0f, 0f,
                _classNames);

            Assert.Single(results);
            Assert.Equal(0, results[0].Box.X);
            Assert.Equal(0, results[0].Box.Y);
            Assert.Equal(40, results[0].Box.Width);
            Assert.Equal(40, results[0].Box.Height);
        }
    }
}
