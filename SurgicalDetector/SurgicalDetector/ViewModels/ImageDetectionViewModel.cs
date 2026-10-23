using SurgicalDetector.Infrastructure;
using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Brushes = System.Drawing.Brushes;
using Pen = System.Drawing.Pen;
namespace SurgicalDetector.ViewModels
{
    public class ImageDetectionViewModel : BaseViewModel
    {
        private readonly IDetectionService _detectionService;
        private readonly IUIService _uiService;
        private readonly IInstrumentService _instrumentService;
        private readonly IGenAIService _genAIService;
    private System.Windows.Media.ImageSource _resultImageSource;
        private bool _hasResults;

        public ObservableCollection<DetectedObjectItem> DetectedItems { get; } = new ObservableCollection<DetectedObjectItem>();

        public System.Windows.Media.ImageSource ResultImageSource
        {
            get => _resultImageSource;
            set => SetProperty(ref _resultImageSource, value);
        }

        public bool HasResults
        {
            get => _hasResults;
            set => SetProperty(ref _hasResults, value);
        }

        public ICommand SelectImageCommand { get; }

        public ImageDetectionViewModel(
            IDetectionService detectionService,
            IUIService uiService,
            IInstrumentService instrumentService,
            IGenAIService genAIService)
        {
            _detectionService = detectionService;
            _uiService = uiService;
            _instrumentService = instrumentService;
            _genAIService = genAIService;
            SelectImageCommand = new RelayCommand(OnSelectImage);
        }

        private void OnSelectImage(object parameter)
        {
            string imagePath = _uiService.ShowOpenFileDialog("Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg");
            if (string.IsNullOrEmpty(imagePath))
            {
                return;
            }

            try
            {
                DetectedItems.Clear();
                HasResults = false;

                using var originalBitmap = new Bitmap(imagePath);

                // Increment Global Stats
                App.GlobalScanCount++;

                var detections = _detectionService.DetectObjects(originalBitmap);

                var allInstruments = _instrumentService.GetAll();

                foreach (var detection in detections)
                {
                    var cropRect = detection.Box;
                    cropRect.Intersect(new Rectangle(0, 0, originalBitmap.Width, originalBitmap.Height));

                    if (cropRect.Width <= 0 || cropRect.Height <= 0) continue;

                    Bitmap croppedBitmap = originalBitmap.Clone(cropRect, originalBitmap.PixelFormat);
                    var thumbnail = ConvertBitmapToBitmapImage(croppedBitmap);
                    croppedBitmap.Dispose();

                    var dbInstrument = allInstruments.FirstOrDefault(i => i.Name.Equals(detection.Label, StringComparison.OrdinalIgnoreCase));

                    var mediaColor = System.Windows.Media.Color.FromArgb(
                            detection.Color.A,
                            detection.Color.R,
                            detection.Color.G,
                            detection.Color.B);

                    var brush = new System.Windows.Media.SolidColorBrush(mediaColor);
                    brush.Freeze();

                    var item = new DetectedObjectItem(_genAIService)
                    {
                        Name = detection.Label,
                        Confidence = detection.Confidence,
                        BorderColor = mediaColor,
                        ColorBrush = brush,
                        Thumbnail = thumbnail,
                        Category = dbInstrument?.Category ?? "Unknown Category",
                        LocalDescription = dbInstrument?.Description ?? "No local database description available for this item."
                    };
                    DetectedItems.Add(item);
                }

                var finalBitmap = DrawDetections(originalBitmap, detections);
                ResultImageSource = ConvertBitmapToBitmapImage(finalBitmap);
                HasResults = DetectedItems.Count > 0;
            }
            catch (Exception ex)
            {
                _uiService.ShowError($"An error occurred during detection: {ex.Message}");
            }
        }

        private Bitmap DrawDetections(Bitmap originalImage, System.Collections.Generic.List<DetectionResult> detections)
        {
            var finalImage = new Bitmap(originalImage.Width, originalImage.Height);
            using (var g = Graphics.FromImage(finalImage))
            {
                g.DrawImage(originalImage, 0, 0);
                foreach (var detection in detections)
                {
                    using var pen = new Pen(detection.Color, 5);
                    g.DrawRectangle(pen, detection.Box);
                }
            }
            return finalImage;
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
    }
}