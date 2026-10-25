using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System.Drawing;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace SurgicalDetector.Tests
{
    public class InferenceBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public InferenceBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BenchmarkRunner_ExecutesSuccessfullyAndReportsRealisticLatencies()
        {
            string modelPath = "best.onnx";
            string resolvedModel = OnnxDetectionService.ResolveModelPath(modelPath);

            string imagePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Images", "Test", "test_image.jpg");
            if (!File.Exists(imagePath))
            {
                imagePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test_image.jpg");
            }

            if (!File.Exists(resolvedModel) || !File.Exists(imagePath))
            {
                // Skip if binary assets not colocated in test directory
                return;
            }

            using var image = new Bitmap(imagePath);
            var options = new InferenceOptions
            {
                ConfidenceThreshold = 0.5f,
                NmsThreshold = 0.45f,
                TargetWidth = 640,
                TargetHeight = 640,
                UseLetterbox = true
            };

            // Run benchmark with 2 warmup runs and 10 measured runs for fast automated test
            var result = InferenceBenchmarkService.RunBenchmark(resolvedModel, image, options, warmupRuns: 2, measuredRuns: 10);

            _output.WriteLine(result.ToString());

            Assert.NotNull(result);
            Assert.Equal("CPUExecutionProvider", result.ActiveExecutionProvider);
            Assert.True(result.ModelInitializationMs > 0, "Model initialization time must be > 0");
            Assert.True(result.ColdStartLatencyMs > 0, "Cold start latency must be > 0");
            Assert.True(result.MeanPreprocessingMs > 0, "Preprocessing time must be > 0");
            Assert.True(result.MeanInferenceMs > 0, "Inference time must be > 0");
            Assert.True(result.MeanEndToEndMs > 0, "End-to-end latency must be > 0");
            Assert.True(result.SteadyStateFps > 0, "Steady-state FPS must be > 0");
        }
    }
}
