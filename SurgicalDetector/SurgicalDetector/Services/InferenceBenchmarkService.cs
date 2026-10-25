using SurgicalDetector.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SurgicalDetector.Services
{
    public class BenchmarkResult
    {
        public double ModelInitializationMs { get; set; }
        public double ColdStartLatencyMs { get; set; }
        public int WarmupIterations { get; set; }
        public int MeasuredIterations { get; set; }

        public double MeanPreprocessingMs { get; set; }
        public double MeanInferenceMs { get; set; }
        public double MeanPostprocessingMs { get; set; }
        public double MeanEndToEndMs { get; set; }

        public double MedianEndToEndMs { get; set; }
        public double MinEndToEndMs { get; set; }
        public double MaxEndToEndMs { get; set; }
        public double P95EndToEndMs { get; set; }

        public double SteadyStateFps => MeanEndToEndMs > 0 ? (1000.0 / MeanEndToEndMs) : 0;

        public string ActiveExecutionProvider { get; set; } = "Unknown";
        public Size ImageDimensions { get; set; }
        public Size ModelCanvasDimensions { get; set; }

        public string OperatingSystem { get; set; } = RuntimeInformation.OSDescription;
        public string Architecture { get; set; } = RuntimeInformation.ProcessArchitecture.ToString();
        public int ProcessorCount { get; set; } = Environment.ProcessorCount;
        public string DotNetVersion { get; set; } = Environment.Version.ToString();

        public override string ToString()
        {
            return $"=================================================================\n" +
                   $"        YOLOv11 ONNX INFERENCE BENCHMARK REPORT (.NET 8)        \n" +
                   $"=================================================================\n" +
                   $"Active Provider:         {ActiveExecutionProvider}\n" +
                   $"Input Image Size:        {ImageDimensions.Width}x{ImageDimensions.Height}\n" +
                   $"Model Canvas:            {ModelCanvasDimensions.Width}x{ModelCanvasDimensions.Height}\n" +
                   $"OS / Architecture:       {OperatingSystem} ({Architecture})\n" +
                   $"CPU Cores:               {ProcessorCount} logical processors\n" +
                   $"Runtime:                 .NET {DotNetVersion}\n" +
                   $"-----------------------------------------------------------------\n" +
                   $"Model Initialization:    {ModelInitializationMs:F2} ms\n" +
                   $"Cold-Start Latency:      {ColdStartLatencyMs:F2} ms\n" +
                   $"Warm-up Iterations:      {WarmupIterations}\n" +
                   $"Measured Iterations:     {MeasuredIterations}\n" +
                   $"-----------------------------------------------------------------\n" +
                   $"Mean Preprocessing:      {MeanPreprocessingMs:F2} ms\n" +
                   $"Mean ONNX Inference:     {MeanInferenceMs:F2} ms\n" +
                   $"Mean Postprocessing/NMS: {MeanPostprocessingMs:F2} ms\n" +
                   $"Mean End-to-End Latency: {MeanEndToEndMs:F2} ms\n" +
                   $"Median End-to-End:       {MedianEndToEndMs:F2} ms\n" +
                   $"P95 End-to-End:          {P95EndToEndMs:F2} ms\n" +
                   $"Min / Max Latency:       {MinEndToEndMs:F2} ms / {MaxEndToEndMs:F2} ms\n" +
                   $"Steady-State Throughput: {SteadyStateFps:F1} FPS\n" +
                   $"=================================================================";
        }
    }

    /// <summary>
    /// Precision benchmarking utility for the YOLOv11 ONNX Runtime pipeline.
    /// Uses monotonic Stopwatch timers and separates cold-start, warm-up, and steady-state timings.
    /// </summary>
    public static class InferenceBenchmarkService
    {
        public static BenchmarkResult RunBenchmark(
            string modelPath,
            Bitmap testImage,
            InferenceOptions? options = null,
            int warmupRuns = 5,
            int measuredRuns = 30)
        {
            if (testImage == null) throw new ArgumentNullException(nameof(testImage));

            var result = new BenchmarkResult
            {
                WarmupIterations = warmupRuns,
                MeasuredIterations = measuredRuns,
                ImageDimensions = testImage.Size,
                ModelCanvasDimensions = new Size(options?.TargetWidth ?? 640, options?.TargetHeight ?? 640)
            };

            var resolvedModelPath = OnnxDetectionService.ResolveModelPath(modelPath);
            if (!File.Exists(resolvedModelPath))
            {
                throw new FileNotFoundException($"Model artifact not found for benchmark at: {resolvedModelPath}");
            }

            // 1. Measure Model Initialization Latency
            var initSw = Stopwatch.StartNew();
            using var service = new OnnxDetectionService(resolvedModelPath, options);
            initSw.Stop();
            result.ModelInitializationMs = initSw.Elapsed.TotalMilliseconds;
            result.ActiveExecutionProvider = service.ActiveExecutionProvider;

            var opt = options?.Clone() ?? InferenceOptions.Default;

            // 2. Measure Cold-Start Latency (First End-to-End Pass)
            var coldSw = Stopwatch.StartNew();
            var coldDetections = service.DetectObjects(testImage, opt);
            coldSw.Stop();
            result.ColdStartLatencyMs = coldSw.Elapsed.TotalMilliseconds;

            // 3. Warm-up Iterations (Stabilize CPU frequency, JIT compilation, and ORT memory pools)
            for (int i = 0; i < warmupRuns; i++)
            {
                service.DetectObjects(testImage, opt);
            }

            // 4. Measured Steady-State Iterations with Per-Stage Monotonic Timings
            var prepTimes = new List<double>(measuredRuns);
            var inferTimes = new List<double>(measuredRuns);
            var postTimes = new List<double>(measuredRuns);
            var totalTimes = new List<double>(measuredRuns);

            var sw = new Stopwatch();

            for (int i = 0; i < measuredRuns; i++)
            {
                sw.Restart();

                // Stage 1: Preprocessing
                var tensor = OnnxDetectionService.PreprocessImage(
                    testImage,
                    opt.TargetWidth,
                    opt.TargetHeight,
                    opt.UseLetterbox,
                    out float scale,
                    out float padLeft,
                    out float padTop);
                long prepTicks = sw.ElapsedTicks;

                // Stage 2: Native ONNX Inference Session
                sw.Restart();
                // Access inference through public service invocation or internal timing
                using var modelOutput = RunInferenceInternal(service, tensor);
                long inferTicks = sw.ElapsedTicks;

                // Stage 3: Postprocessing & Class-Aware NMS
                sw.Restart();
                var parsed = OnnxDetectionService.ParseOutput(
                    modelOutput.AsTensor<float>(),
                    testImage.Size,
                    opt,
                    scale,
                    padLeft,
                    padTop,
                    service.ClassNames);
                var surviving = OnnxDetectionService.ApplyNMS(
                    parsed,
                    opt.NmsThreshold,
                    opt.ClassAgnosticNms,
                    opt.MaxDetections);
                long postTicks = sw.ElapsedTicks;

                double prepMs = (double)prepTicks / Stopwatch.Frequency * 1000.0;
                double inferMs = (double)inferTicks / Stopwatch.Frequency * 1000.0;
                double postMs = (double)postTicks / Stopwatch.Frequency * 1000.0;
                double totalMs = prepMs + inferMs + postMs;

                prepTimes.Add(prepMs);
                inferTimes.Add(inferMs);
                postTimes.Add(postMs);
                totalTimes.Add(totalMs);
            }

            // 5. Aggregate Statistics
            totalTimes.Sort();

            result.MeanPreprocessingMs = prepTimes.Average();
            result.MeanInferenceMs = inferTimes.Average();
            result.MeanPostprocessingMs = postTimes.Average();
            result.MeanEndToEndMs = totalTimes.Average();

            result.MinEndToEndMs = totalTimes.First();
            result.MaxEndToEndMs = totalTimes.Last();
            result.MedianEndToEndMs = totalTimes[totalTimes.Count / 2];

            int p95Index = (int)Math.Ceiling(totalTimes.Count * 0.95) - 1;
            result.P95EndToEndMs = totalTimes[Math.Clamp(p95Index, 0, totalTimes.Count - 1)];

            return result;
        }

        private static Microsoft.ML.OnnxRuntime.DisposableNamedOnnxValue RunInferenceInternal(
            OnnxDetectionService service,
            Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float> tensor)
        {
            var method = typeof(OnnxDetectionService).GetMethod(
                "RunInference",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException("RunInference method not found on OnnxDetectionService.");
            }

            return (Microsoft.ML.OnnxRuntime.DisposableNamedOnnxValue)method.Invoke(service, new object[] { tensor })!;
        }
    }
}
