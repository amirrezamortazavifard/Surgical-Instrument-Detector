using SurgicalDetector.Models;
using System.Collections.Generic;
using System.Drawing;

namespace SurgicalDetector.Services
{
    public interface IDetectionService
    {
        List<DetectionResult> DetectObjects(Bitmap image);
    }
}