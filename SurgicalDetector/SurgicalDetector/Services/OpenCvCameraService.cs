using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace SurgicalDetector.Services
{
    public class OpenCvCameraService : ICameraService
    {
        private VideoCapture _capture;
        private CancellationTokenSource _cancellationTokenSource;

        public event Action<Bitmap> FrameCaptured;

        public List<string> GetAvailableCameras()
        {
            var cameras = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                using var tempCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (tempCapture.IsOpened())
                {
                    cameras.Add($"Camera {i}");
                }
            }
            return cameras;
        }

        public void StartCapture(int cameraIndex)
        {
            if (_capture?.IsOpened() ?? false)
            {
                StopCapture();
            }

            _capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            if (!_capture.IsOpened())
            {
                throw new InvalidOperationException("Failed to open the selected camera.");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => CaptureLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        private async Task CaptureLoop(CancellationToken token)
        {
            using var frame = new Mat();
            while (!token.IsCancellationRequested)
            {
                if (_capture.Read(frame) && !frame.Empty())
                {
                    FrameCaptured?.Invoke(BitmapConverter.ToBitmap(frame));
                }
                await Task.Delay(10, token);
            }
        }

        public void StopCapture()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}