using System;
using System.Collections.Generic;
using System.IO;

namespace SurgicalDetector.Models
{
    public class SurgicalInstrument
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> KeyFeatures { get; set; }

        public string FullImagePath => !string.IsNullOrEmpty(ImagePath) ? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ImagePath)) : null;
    }
}