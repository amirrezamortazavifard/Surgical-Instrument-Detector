using System;
using System.Collections.Generic;
using System.Drawing;

namespace SurgicalDetector.Services
{
    public interface ICameraService : IDisposable
    {
        event Action<Bitmap> FrameCaptured;
        List<string> GetAvailableCameras();
        void StartCapture(int cameraIndex);
        void StopCapture();
    }
}