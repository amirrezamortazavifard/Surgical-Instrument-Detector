using SurgicalDetector.Models;
using System.Collections.Generic;
using System.Drawing;

namespace SurgicalDetector.Services
{
    public interface IDetectionService
    {
        List<DetectionResult> DetectObjects(Bitmap image);
        List<DetectionResult> DetectObjects(Bitmap image, InferenceOptions options);
        List<DetectionResult> DetectObjects(Bitmap image, float confidenceThreshold, float nmsThreshold);
    }
}