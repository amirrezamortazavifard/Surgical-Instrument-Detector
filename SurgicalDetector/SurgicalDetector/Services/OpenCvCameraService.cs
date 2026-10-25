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
        private readonly object _lock = new object();
        private VideoCapture? _capture;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<Bitmap>? FrameCaptured;

        public List<string> GetAvailableCameras()
        {
            var cameras = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    using var tempCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                    if (tempCapture.IsOpened())
                    {
                        cameras.Add($"Camera {i}");
                    }
                }
                catch
                {
                    // Continue scanning other indexes
                }
            }
            return cameras;
        }

        public void StartCapture(int cameraIndex)
        {
            lock (_lock)
            {
                if (_capture?.IsOpened() ?? false)
                {
                    StopCapture();
                }

                try
                {
                    _capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
                    if (!_capture.IsOpened())
                    {
                        StopCapture();
                        throw new InvalidOperationException($"Failed to open camera device index {cameraIndex}.");
                    }

                    // Prevent DirectShow from queuing stale frames in hardware buffer
                    try
                    {
                        _capture.Set(VideoCaptureProperties.BufferSize, 1);
                    }
                    catch
                    {
                        // Some camera drivers ignore buffer size property
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                    var token = _cancellationTokenSource.Token;
                    Task.Run(() => CaptureLoop(token), token);
                }
                catch
                {
                    StopCapture();
                    throw;
                }
            }
        }

        private async Task CaptureLoop(CancellationToken token)
        {
            using var frame = new Mat();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool readSuccess = false;
                    lock (_lock)
                    {
                        if (_capture != null && _capture.IsOpened())
                        {
                            readSuccess = _capture.Read(frame);
                        }
                    }

                    if (readSuccess && !frame.Empty())
                    {
                        var bitmap = BitmapConverter.ToBitmap(frame);
                        FrameCaptured?.Invoke(bitmap);
                    }

                    await Task.Delay(15, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Camera capture exception: {ex.Message}");
                    await Task.Delay(50, token).ConfigureAwait(false);
                }
            }
        }

        public void StopCapture()
        {
            lock (_lock)
            {
                try
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();
                }
                catch { }
                finally
                {
                    _cancellationTokenSource = null;
                }

                try
                {
                    if (_capture != null)
                    {
                        _capture.Release();
                        _capture.Dispose();
                    }
                }
                catch { }
                finally
                {
                    _capture = null;
                }
            }
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}