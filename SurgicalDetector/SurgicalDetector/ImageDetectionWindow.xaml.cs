using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

using GD = System.Drawing;

namespace SurgicalDetector
{
    public partial class ImageDetectionWindow : Window
    {
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

        public ImageDetectionWindow()
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

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var originalBitmap = new GD.Bitmap(openFileDialog.FileName))
                    {
                        var preprocessedTensor = PreprocessImage(originalBitmap);
                        using (var modelOutput = RunInference(preprocessedTensor))
                        {
                            var finalImage = PostprocessAndDraw(originalBitmap, modelOutput);
                            ResultImage.Source = ConvertBitmapToBitmapImage(finalImage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during detection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        private DisposableNamedOnnxValue RunInference(DenseTensor<float> inputTensor)
        {
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(ModelInputName, inputTensor) };
            var results = _session.Run(inputs);
            return results.First(r => r.Name == ModelOutputName);
        }

        private GD.Bitmap PostprocessAndDraw(GD.Bitmap originalImage, DisposableNamedOnnxValue modelOutput)
        {
            var outputTensor = modelOutput.AsTensor<float>();
            var detections = ParseOutput(outputTensor, originalImage.Size);
            var finalDetections = ApplyNMS(detections);

            var finalImage = new GD.Bitmap(originalImage.Width, originalImage.Height);
            using (var g = GD.Graphics.FromImage(finalImage))
            {
                g.DrawImage(originalImage, 0, 0);
                foreach (var detection in finalDetections)
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
            return finalImage;
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
                    float currentConfidence = output[0, 4 + j, i];
                    if (currentConfidence > maxConfidence)
                    {
                        maxConfidence = currentConfidence;
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

            float intersectionArea = Math.Max(0, xB - xA + 1) * Math.Max(0, yB - yA + 1);
            if (intersectionArea == 0) return 0;

            float boxAArea = boxA.Width * boxA.Height;
            float boxBArea = boxB.Width * boxB.Height;

            return intersectionArea / (boxAArea + boxBArea - intersectionArea);
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
    }

    public class DetectionResult
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