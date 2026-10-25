using SurgicalDetector.Infrastructure;
using SurgicalDetector.Models;
using System.Drawing;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class ConfigurationAndPaletteTests
    {
        [Fact]
        public void ConfigurationService_DefaultThresholds_AreSetSensibly()
        {
            var config = new ConfigurationService();

            Assert.InRange(config.ConfidenceThreshold, 0.1f, 0.9f);
            Assert.InRange(config.NmsThreshold, 0.1f, 0.9f);
            Assert.True(config.UseLetterbox);
        }

        [Fact]
        public void ConfigurationService_Thresholds_AreClampedToSafeRanges()
        {
            var config = new ConfigurationService();

            config.ConfidenceThreshold = -0.5f;
            Assert.True(config.ConfidenceThreshold >= 0.05f);

            config.ConfidenceThreshold = 1.5f;
            Assert.True(config.ConfidenceThreshold <= 0.99f);

            config.NmsThreshold = -0.1f;
            Assert.True(config.NmsThreshold >= 0.05f);

            config.NmsThreshold = 2.0f;
            Assert.True(config.NmsThreshold <= 0.99f);
        }

        [Fact]
        public void DetectionResult_StandardClasses_HaveDeterministicColors()
        {
            var scalpelColor1 = DetectionResult.GetColorForLabel("Scalpel");
            var scalpelColor2 = DetectionResult.GetColorForLabel("Scalpel");
            Assert.Equal(scalpelColor1, scalpelColor2);
            Assert.Equal(Color.FromArgb(16, 185, 129), scalpelColor1);

            var clampColor = DetectionResult.GetColorForLabel("Straight Dissection Clamp");
            Assert.Equal(Color.FromArgb(59, 130, 246), clampColor);

            var straightScissorColor = DetectionResult.GetColorForLabel("Straight Mayo Scissor");
            Assert.Equal(Color.FromArgb(245, 158, 11), straightScissorColor);

            var curvedScissorColor = DetectionResult.GetColorForLabel("Curved Mayo Scissor");
            Assert.Equal(Color.FromArgb(236, 72, 153), curvedScissorColor);
        }
    }
}
