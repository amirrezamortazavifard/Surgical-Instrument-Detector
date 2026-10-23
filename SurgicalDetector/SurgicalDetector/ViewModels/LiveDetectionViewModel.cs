using SurgicalDetector.Infrastructure;
using SurgicalDetector.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
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
    public class LiveDetectionViewModel : BaseViewModel, IDisposable
    {
        private readonly IDetectionService _detectionService;
        private readonly ICameraService _cameraService;

        private ImageSource _liveImageSource;
        private bool _isCameraRunning;
        private int _selectedCameraIndex;

        public ObservableCollection<string> AvailableCameras { get; } = new ObservableCollection<string>();
        public ICommand ToggleCameraCommand { get; }
        public ICommand WindowClosingCommand { get; }

        public ImageSource LiveImageSource
        {
            get => _liveImageSource;
            set => SetProperty(ref _liveImageSource, value);
        }

        public bool IsCameraRunning
        {
            get => _isCameraRunning;
            set
            {
                SetProperty(ref _isCameraRunning, value);
                OnPropertyChanged(nameof(StartButtonContent));
                OnPropertyChanged(nameof(IsCameraSelectionEnabled));
            }
        }

        public int SelectedCameraIndex
        {
            get => _selectedCameraIndex;
            set => SetProperty(ref _selectedCameraIndex, value);
        }

        public string StartButtonContent => IsCameraRunning ? "Stop Camera" : "Start Camera";
        public bool IsCameraSelectionEnabled => !IsCameraRunning;

        public LiveDetectionViewModel(IDetectionService detectionService, ICameraService cameraService, IUIService uiService)
        {
            _detectionService = detectionService;
            _cameraService = cameraService;
            _cameraService.FrameCaptured += OnFrameCaptured;

            ToggleCameraCommand = new RelayCommand(OnToggleCamera, p => SelectedCameraIndex >= 0);
            WindowClosingCommand = new RelayCommand(p => Dispose());

            LoadAvailableCameras(uiService);
        }

        private void LoadAvailableCameras(IUIService uiService)
        {
            try
            {
                var cameras = _cameraService.GetAvailableCameras();
                if (cameras.Count == 0)
                {
                    uiService.ShowError("No cameras found!");
                    return;
                }

                foreach (var camera in cameras)
                {
                    AvailableCameras.Add(camera);
                }
                SelectedCameraIndex = 0;
            }
            catch (Exception ex)
            {
                uiService.ShowError($"Error finding cameras: {ex.Message}");
            }
        }

        private void OnToggleCamera(object parameter)
        {
            if (IsCameraRunning)
            {
                _cameraService.StopCapture();
                IsCameraRunning = false;
            }
            else
            {
                _cameraService.StartCapture(SelectedCameraIndex);
                IsCameraRunning = true;
            }
        }

        private void OnFrameCaptured(Bitmap frame)
        {
            var detections = _detectionService.DetectObjects(frame);
            DrawDetections(frame, detections);

            var imageSource = ConvertBitmapToBitmapImage(frame);
            frame.Dispose();

            Application.Current.Dispatcher.Invoke(() =>
            {
                LiveImageSource = imageSource;
            });
        }

        private void DrawDetections(Bitmap image, List<Models.DetectionResult> detections)
        {
            using (var g = Graphics.FromImage(image))
            {
                foreach (var detection in detections)
                {
                    using var pen = new Pen(detection.Color, 2);
                    g.DrawRectangle(pen, detection.Box);

                    string label = $"{detection.Label} ({detection.Confidence:P0})";
                    using var font = new Font("Arial", 12, System.Drawing.FontStyle.Bold);

                    var textSize = g.MeasureString(label, font);
                    var textLocation = new Point(detection.Box.X, detection.Box.Y - (int)textSize.Height);
                    if (textLocation.Y < 0) textLocation.Y = detection.Box.Y;

                    using var brush = new SolidBrush(detection.Color);
                    g.FillRectangle(brush, new Rectangle(textLocation, Size.Ceiling(textSize)));
                    g.DrawString(label, font, Brushes.White, textLocation);
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
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        public void Dispose()
        {
            _cameraService.StopCapture();
        }
    }
}