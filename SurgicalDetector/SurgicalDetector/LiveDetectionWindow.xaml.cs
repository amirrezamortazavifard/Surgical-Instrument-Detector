using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using GD = System.Drawing;

namespace SurgicalDetector
{
    public partial class LiveDetectionWindow : System.Windows.Window
    {
       
        private VideoCapture _capture;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isCameraRunning = false;

       
        private readonly InferenceSession _session;
        private const string ModelInputName = "images";
        private const string ModelOutputName = "output0";
        private const int TargetWidth = 640;
        private const int TargetHeight = 640;

        private readonly List<string> _classNames = new List<string>
        {
            "Scalpel",
            "Straight Dissection Clamp",
            "Straight Mayo Scissor",
            "Curved Mayo Scissor"
        };

        private const float ConfidenceThreshold = 0.5f;
        private const float NmsThreshold = 0.45f;

        public LiveDetectionWindow()
        {
            InitializeComponent();

            try
            {
                _session = new InferenceSession("best.onnx");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading ONNX model: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var cameraIndexes = GetAvailableCameraIndexes();
            if (cameraIndexes.Length == 0)
            {
                MessageBox.Show("No cameras found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = false;
                return;
            }

            foreach (var index in cameraIndexes)
            {
                CameraComboBox.Items.Add($"Camera {index}");
            }
            CameraComboBox.SelectedIndex = 0;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCameraRunning)
            {
                int cameraIndex = CameraComboBox.SelectedIndex;
                if (cameraIndex < 0) return;

                _capture = new VideoCapture(cameraIndex);
                if (!_capture.IsOpened())
                {
                    MessageBox.Show("Failed to open the selected camera.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ProcessCameraFeedAsync(_cancellationTokenSource.Token));

                _isCameraRunning = true;
                StartButton.Content = "Stop Camera";
                CameraComboBox.IsEnabled = false;
            }
            else
            {
                _cancellationTokenSource?.Cancel();
                _isCameraRunning = false;
                StartButton.Content = "Start Camera";
                CameraComboBox.IsEnabled = true;
            }
        }

        private async Task ProcessCameraFeedAsync(CancellationToken token)
        {
            using (var frame = new Mat())
            {
                while (!token.IsCancellationRequested)
                {
                    if (!_capture.Read(frame) || frame.Empty())
                    {
                        await Task.Delay(10, token);
                        continue;
                    }

                    using (var frameBitmap = BitmapConverter.ToBitmap(frame))
                    {
                        var inputTensor = PreprocessImage(frameBitmap);

                        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(ModelInputName, inputTensor) };
                        using (var results = _session.Run(inputs))
                        {
                            var outputTensor = results.First(r => r.Name == ModelOutputName).AsTensor<float>();

                            var detections = ParseOutput(outputTensor, frameBitmap.Size);
                            var finalDetections = ApplyNMS(detections);

                            DrawDetections(frameBitmap, finalDetections);
                        }

                        var finalImageSource = ConvertBitmapToBitmapImage(frameBitmap);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LiveImage.Source = finalImageSource;
                        });
                    }

                    await Task.Delay(1, token); 
                }
            }
            _capture?.Release();
        }


        private DenseTensor<float> PreprocessImage(GD.Bitmap image)
        {
            using (var resizedImage = new GD.Bitmap(TargetWidth, TargetHeight))
            {
                using (var graphics = GD.Graphics.FromImage(resizedImage))
                {
                    graphics.CompositingQuality = GD.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = GD.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, 0, 0, TargetWidth, TargetHeight);
                }

                var tensor = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });
                for (int y = 0; y < TargetHeight; y++)
                {
                    for (int x = 0; x < TargetWidth; x++)
                    {
                        var pixel = resizedImage.GetPixel(x, y);
                        tensor[0, 0, y, x] = pixel.R / 255.0f;
                        tensor[0, 1, y, x] = pixel.G / 255.0f;
                        tensor[0, 2, y, x] = pixel.B / 255.0f;
                    }
                }
                return tensor;
            }
        }

        private List<DetectionResult> ParseOutput(Tensor<float> output, GD.Size originalSize)
        {
            var results = new List<DetectionResult>();
            int numClasses = _classNames.Count;
            int numDetections = output.Dimensions[2];

            float scaleX = (float)originalSize.Width / TargetWidth;
            float scaleY = (float)originalSize.Height / TargetHeight;

            for (int i = 0; i < numDetections; i++)
            {
                float maxConfidence = 0;
                int classIndex = 0;
                for (int j = 0; j < numClasses; j++)
                {
                    if (output[0, 4 + j, i] > maxConfidence)
                    {
                        maxConfidence = output[0, 4 + j, i];
                        classIndex = j;
                    }
                }

                if (maxConfidence < ConfidenceThreshold) continue;

                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                int x1 = (int)((cx - w / 2) * scaleX);
                int y1 = (int)((cy - h / 2) * scaleY);
                int width = (int)(w * scaleX);
                int height = (int)(h * scaleY);

                results.Add(new DetectionResult
                {
                    Box = new GD.Rectangle(x1, y1, width, height),
                    Confidence = maxConfidence,
                    Label = _classNames[classIndex]
                });
            }
            return results;
        }

        private List<DetectionResult> ApplyNMS(List<DetectionResult> detections)
        {
            var finalDetections = new List<DetectionResult>();
            var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();

            while (sortedDetections.Any())
            {
                var bestDetection = sortedDetections.First();
                finalDetections.Add(bestDetection);
                sortedDetections.Remove(bestDetection);

                for (int i = sortedDetections.Count - 1; i >= 0; i--)
                {
                    if (CalculateIoU(bestDetection.Box, sortedDetections[i].Box) > NmsThreshold)
                    {
                        sortedDetections.RemoveAt(i);
                    }
                }
            }
            return finalDetections;
        }

        private float CalculateIoU(GD.Rectangle boxA, GD.Rectangle boxB)
        {
            int xA = Math.Max(boxA.Left, boxB.Left);
            int yA = Math.Max(boxA.Top, boxB.Top);
            int xB = Math.Min(boxA.Right, boxB.Right);
            int yB = Math.Min(boxA.Bottom, boxB.Bottom);
            float intersectionArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            if (intersectionArea == 0) return 0;
            float boxAArea = boxA.Width * boxA.Height;
            float boxBArea = boxB.Width * boxB.Height;
            return intersectionArea / (boxAArea + boxBArea - intersectionArea);
        }

        private void DrawDetections(GD.Bitmap image, List<DetectionResult> detections)
        {
            using (var g = GD.Graphics.FromImage(image))
            {
                foreach (var detection in detections)
                {
                    g.DrawRectangle(new GD.Pen(detection.Color, 2), detection.Box);
                    string label = $"{detection.Label} ({detection.Confidence:P0})";
                    using (var font = new GD.Font("Arial", 12, GD.FontStyle.Bold))
                    {
                        var textSize = g.MeasureString(label, font);
                        var textLocation = new GD.Point(detection.Box.X, detection.Box.Y - (int)textSize.Height);
                        if (textLocation.Y < 0) textLocation.Y = detection.Box.Y;

                        g.FillRectangle(new GD.SolidBrush(detection.Color), new GD.Rectangle(textLocation, GD.Size.Ceiling(textSize)));
                        g.DrawString(label, font, GD.Brushes.White, textLocation);
                    }
                }
            }
        }

        private BitmapImage ConvertBitmapToBitmapImage(GD.Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, GD.Imaging.ImageFormat.Bmp);
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private int[] GetAvailableCameraIndexes()
        {
            var indexes = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                using (var tempCapture = new VideoCapture(i))
                {
                    if (tempCapture.IsOpened())
                    {
                        indexes.Add(i);
                    }
                }
            }
            return indexes.ToArray();
        }
    }
    
    public class LiveDetectionResult
    {
        public GD.Rectangle Box { get; set; }
        public string Label { get; set; }
        public float Confidence { get; set; }
        public GD.Color Color => GetColorForLabel(Label);

        private static readonly Dictionary<string, GD.Color> ColorCache = new Dictionary<string, GD.Color>();
        private static readonly Random Rng = new Random();

        private static GD.Color GetColorForLabel(string label)
        {
            if (ColorCache.TryGetValue(label, out var color)) return color;
            color = GD.Color.FromArgb(Rng.Next(150, 256), Rng.Next(100, 256), Rng.Next(50, 256));
            ColorCache[label] = color;
            return color;
        }
    }
}