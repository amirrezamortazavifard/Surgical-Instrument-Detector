using System;
using System.Collections.Generic;
using System.Drawing;

namespace SurgicalDetector.Models
{
    public class DetectionResult
    {
        public Rectangle Box { get; set; }
        public string Label { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public Color Color => GetColorForLabel(Label);

        private static readonly Dictionary<string, Color> StandardClassColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Scalpel", Color.FromArgb(16, 185, 129) },                  // Emerald Green (#10B981)
            { "Straight Dissection Clamp", Color.FromArgb(59, 130, 246) }, // Electric Blue (#3B82F6)
            { "Straight Mayo Scissor", Color.FromArgb(245, 158, 11) },    // Amber Gold (#F59E0B)
            { "Curved Mayo Scissor", Color.FromArgb(236, 72, 153) }       // Magenta Rose (#EC4899)
        };

        public static Color GetColorForLabel(string label)
        {
            if (!string.IsNullOrEmpty(label) && StandardClassColors.TryGetValue(label, out var color))
            {
                return color;
            }
            return Color.FromArgb(139, 92, 246); // Default Violet (#8B5CF6)
        }
    }
}