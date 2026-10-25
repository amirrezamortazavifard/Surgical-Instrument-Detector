using System;

namespace SurgicalDetector.Models
{
    /// <summary>
    /// Strongly-typed configuration options for the YOLOv11 ONNX inference pipeline.
    /// Controls preprocessing resolution, confidence thresholds, NMS behavior, and detection limits.
    /// </summary>
    public class InferenceOptions
    {
        private float _confidenceThreshold = 0.50f;
        private float _nmsThreshold = 0.45f;
        private int _targetWidth = 640;
        private int _targetHeight = 640;
        private int _maxDetections = 300;

        /// <summary>
        /// Minimum confidence score [0.01, 0.99] required to propose a detection candidate.
        /// </summary>
        public float ConfidenceThreshold
        {
            get => _confidenceThreshold;
            set => _confidenceThreshold = Math.Clamp(value, 0.01f, 0.99f);
        }

        /// <summary>
        /// Intersection-over-Union (IoU) threshold [0.01, 0.99] for Non-Maximum Suppression.
        /// </summary>
        public float NmsThreshold
        {
            get => _nmsThreshold;
            set => _nmsThreshold = Math.Clamp(value, 0.01f, 0.99f);
        }

        /// <summary>
        /// Input canvas width expected by the ONNX model graph. Default is 640.
        /// </summary>
        public int TargetWidth
        {
            get => _targetWidth;
            set => _targetWidth = Math.Max(32, value);
        }

        /// <summary>
        /// Input canvas height expected by the ONNX model graph. Default is 640.
        /// </summary>
        public int TargetHeight
        {
            get => _targetHeight;
            set => _targetHeight = Math.Max(32, value);
        }

        /// <summary>
        /// Maximum number of detections retained following NMS. Default is 300 (Ultralytics standard).
        /// </summary>
        public int MaxDetections
        {
            get => _maxDetections;
            set => _maxDetections = Math.Max(1, value);
        }

        /// <summary>
        /// When true, maintains original aspect ratio via centered padding (letterboxing with RGB 114).
        /// When false, stretches image directly to TargetWidth x TargetHeight.
        /// </summary>
        public bool UseLetterbox { get; set; } = true;

        /// <summary>
        /// When true, NMS suppresses overlapping boxes across all classes (class-agnostic).
        /// When false (default), NMS is class-aware, evaluating suppression within each class label.
        /// </summary>
        public bool ClassAgnosticNms { get; set; } = false;

        /// <summary>
        /// Preferred ONNX Runtime execution provider (e.g. "CPU", "DirectML", "CUDA"). Default is "CPU".
        /// Falls back safely to CPU if the requested provider is unavailable in the active runtime package.
        /// </summary>
        public string ExecutionProvider { get; set; } = "CPU";

        /// <summary>
        /// Hardware device index for GPU execution providers (default 0).
        /// </summary>
        public int DeviceId { get; set; } = 0;

        public static InferenceOptions Default => new InferenceOptions();

        public InferenceOptions Clone()
        {
            return new InferenceOptions
            {
                ConfidenceThreshold = this.ConfidenceThreshold,
                NmsThreshold = this.NmsThreshold,
                TargetWidth = this.TargetWidth,
                TargetHeight = this.TargetHeight,
                MaxDetections = this.MaxDetections,
                UseLetterbox = this.UseLetterbox,
                ClassAgnosticNms = this.ClassAgnosticNms,
                ExecutionProvider = this.ExecutionProvider,
                DeviceId = this.DeviceId
            };
        }
    }
}
