using System.Collections.Generic;
using System.Drawing;
using System;

namespace SurgicalDetector.Models
{
    public class DetectionResult
    {
        public Rectangle Box { get; set; }
        public string Label { get; set; }
        public float Confidence { get; set; }
        public Color Color => GetColorForLabel(Label);

        private static readonly Dictionary<string, Color> ColorCache = new Dictionary<string, Color>();
        private static readonly Random Rng = new Random();

        private static Color GetColorForLabel(string label)
        {
            if (ColorCache.TryGetValue(label, out var color))
            {
                return color;
            }
            color = Color.FromArgb(Rng.Next(150, 256), Rng.Next(100, 256), Rng.Next(50, 256));
            ColorCache[label] = color;
            return color;
        }
    }
}