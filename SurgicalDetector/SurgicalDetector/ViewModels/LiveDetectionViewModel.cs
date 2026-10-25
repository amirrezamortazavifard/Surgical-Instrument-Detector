using SurgicalDetector.Infrastructure;
using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Drawing.Brushes;
using Pen = System.Drawing.Pen;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Font = System.Drawing.Font;

namespace SurgicalDetector.ViewModels
{
    public class LiveDetectionViewModel : BaseViewModel, INavigationAware, IDisposable
    {
        private readonly IDetectionService _detectionService;
        private readonly ICameraService _cameraService;
        private readonly IUIService _uiService;

        private ImageSource? _liveImageSource;
        private bool _isCameraRunning;
        private int _selectedCameraIndex;
        private int _isProcessingFrame = 0;
        private bool _isDisposed = false;

        private Bitmap? _latestFrame;
        private readonly object _frameLock = new object();

        private double _currentLatencyMs = 0;
        private double _currentFps = 0;
        private int _frameCount = 0;
        private readonly Stopwatch _fpsStopwatch = new Stopwatch();

        // Cached drawing resources to avoid per-frame GDI object churn
        private readonly Font _annotationFont = new Font("Arial", 11, System.Drawing.FontStyle.Bold);

        public ObservableCollection<string> AvailableCameras { get; } = new ObservableCollection<string>();
        public ICommand ToggleCameraCommand { get; }
        public ICommand WindowClosingCommand { get; }

        public ImageSource? LiveImageSource
        {
            get => _liveImageSource;
            set => SetProperty(ref _liveImageSource, value);
        }

        public bool IsCameraRunning
        {
            get => _isCameraRunning;
            set
            {
                if (SetProperty(ref _isCameraRunning, value))
                {
                    OnPropertyChanged(nameof(StartButtonContent));
                    OnPropertyChanged(nameof(IsCameraSelectionEnabled));
                    if (!value)
                    {
                        CurrentFps = 0;
                        CurrentLatencyMs = 0;
                    }
                }
            }
        }

        public int SelectedCameraIndex
        {
            get => _selectedCameraIndex;
            set => SetProperty(ref _selectedCameraIndex, value);
        }

        public double CurrentLatencyMs
        {
            get => _currentLatencyMs;
            private set
            {
                if (SetProperty(ref _currentLatencyMs, Math.Round(value, 1)))
                {
                    OnPropertyChanged(nameof(LatencyText));
                }
            }
        }

        public string LatencyText => $"{CurrentLatencyMs:F0} ms";

        public double CurrentFps
        {
            get => _currentFps;
            private set
            {
                if (SetProperty(ref _currentFps, Math.Round(value, 1)))
                {
                    OnPropertyChanged(nameof(FpsText));
                }
            }
        }

        public string FpsText => $"{CurrentFps:F1} FPS";

        public string StartButtonContent => IsCameraRunning ? "Stop Camera" : "Start Camera";
        public bool IsCameraSelectionEnabled => !IsCameraRunning;

        public LiveDetectionViewModel(IDetectionService detectionService, ICameraService cameraService, IUIService uiService)
        {
            _detectionService = detectionService;
            _cameraService = cameraService;
            _uiService = uiService;
            _cameraService.FrameCaptured += OnFrameCaptured;

            ToggleCameraCommand = new RelayCommand(OnToggleCamera, p => SelectedCameraIndex >= 0);
            WindowClosingCommand = new RelayCommand(p => Dispose());

            LoadAvailableCameras();
        }

        private void LoadAvailableCameras()
        {
            try
            {
                var cameras = _cameraService.GetAvailableCameras();
                AvailableCameras.Clear();
                foreach (var camera in cameras)
                {
                    AvailableCameras.Add(camera);
                }

                if (AvailableCameras.Count > 0)
                {
                    SelectedCameraIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _uiService.ShowError($"Error finding cameras: {ex.Message}");
            }
        }

        private void OnToggleCamera(object parameter)
        {
            if (IsCameraRunning)
            {
                StopCaptureInternal();
            }
            else
            {
                StartCaptureInternal();
            }
        }

        private void StartCaptureInternal()
        {
            if (SelectedCameraIndex < 0) return;

            try
            {
                _cameraService.StartCapture(SelectedCameraIndex);
                IsCameraRunning = true;
                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
            catch (Exception ex)
            {
                IsCameraRunning = false;
                _uiService.ShowError($"Failed to start camera device: {ex.Message}");
            }
        }

        private void StopCaptureInternal()
        {
            try
            {
                _cameraService.StopCapture();
            }
            catch { }
            finally
            {
                IsCameraRunning = false;
                _fpsStopwatch.Reset();
                _frameCount = 0;

                // Dispose any pending buffered frame
                var pending = Interlocked.Exchange(ref _latestFrame, null);
                pending?.Dispose();
            }
        }

        /// <summary>
        /// Frame capture event handler implementing bounded latest-frame strategy.
        /// Immediately replaces any unconsumed frame to prevent unbounded latency backlog.
        /// </summary>
        private void OnFrameCaptured(Bitmap frame)
        {
            if (_isDisposed || !IsCameraRunning)
            {
                frame.Dispose();
                return;
            }

            // Atomically stage latest frame and immediately dispose older unconsumed frame
            var stale = Interlocked.Exchange(ref _latestFrame, frame);
            stale?.Dispose();

            // Spawn worker if not already processing
            if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) == 0)
            {
                Task.Run(ProcessFramesWorker);
            }
        }

        private void ProcessFramesWorker()
        {
            try
            {
                while (IsCameraRunning && !_isDisposed)
                {
                    // Retrieve latest available frame
                    var current = Interlocked.Exchange(ref _latestFrame, null);
                    if (current == null)
                    {
                        break;
                    }

                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var detections = _detectionService.DetectObjects(current);
                        DrawDetections(current, detections);
                        sw.Stop();

                        double latency = sw.Elapsed.TotalMilliseconds;
                        var imageSource = ConvertBitmapToBitmapImage(current);

                        // Safely dispatch image and stats updates to WPF UI thread
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            if (IsCameraRunning)
                            {
                                LiveImageSource = imageSource;
                                UpdatePerformanceStats(latency);
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Frame processing error: {ex.Message}");
                    }
                    finally
                    {
                        current.Dispose();
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessingFrame, 0);

                // Check if another frame arrived while exiting the loop
                if (IsCameraRunning && !_isDisposed && _latestFrame != null)
                {
                    if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) == 0)
                    {
                        Task.Run(ProcessFramesWorker);
                    }
                }
            }
        }

        private void UpdatePerformanceStats(double latencyMs)
        {
            CurrentLatencyMs = latencyMs;
            _frameCount++;

            if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                double fps = _frameCount / (_fpsStopwatch.ElapsedMilliseconds / 1000.0);
                CurrentFps = fps;
                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
        }

        private void DrawDetections(Bitmap image, List<DetectionResult> detections)
        {
            using (var g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var detection in detections)
                {
                    using var pen = new Pen(detection.Color, 3);
                    g.DrawRectangle(pen, detection.Box);

                    string label = $"{detection.Label} ({detection.Confidence:P0})";

                    var textSize = g.MeasureString(label, _annotationFont);
                    var textLocation = new Point(detection.Box.X, detection.Box.Y - (int)textSize.Height - 4);
                    if (textLocation.Y < 0) textLocation.Y = detection.Box.Y + 2;

                    using var brush = new SolidBrush(detection.Color);
                    g.FillRectangle(brush, new Rectangle(textLocation, Size.Ceiling(textSize)));
                    g.DrawString(label, _annotationFont, Brushes.White, textLocation);
                }
            }
        }

        private BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Crucial for cross-thread rendering
                return bitmapImage;
            }
        }

        /// <summary>
        /// INavigationAware lifecycle: automatically halts camera capture when user navigates away.
        /// </summary>
        public void OnNavigatedFrom()
        {
            if (IsCameraRunning)
            {
                StopCaptureInternal();
            }
        }

        /// <summary>
        /// INavigationAware lifecycle: refreshes camera list when user navigates to live detection.
        /// </summary>
        public void OnNavigatedTo()
        {
            if (AvailableCameras.Count == 0)
            {
                LoadAvailableCameras();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopCaptureInternal();
            _cameraService.FrameCaptured -= OnFrameCaptured;
            _annotationFont.Dispose();
        }
    }
}