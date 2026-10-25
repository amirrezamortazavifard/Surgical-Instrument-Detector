using SurgicalDetector.Services;
using System.Drawing;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class PreprocessingAndTransformTests
    {
        [Fact]
        public void PreprocessImage_LetterboxScaleAndPads_ComputedAccurately()
        {
            // Input image of 1280x720 (16:9 aspect ratio)
            using var bmp = new Bitmap(1280, 720);

            var tensor = OnnxDetectionService.PreprocessImage(
                bmp,
                targetWidth: 640,
                targetHeight: 640,
                letterbox: true,
                out float scale,
                out float padX,
                out float padY);

            // Scale = min(640/1280, 640/720) = 0.5f
            Assert.Equal(0.5f, scale, 3);

            // Scaled dimensions: 1280 * 0.5 = 640 width, 720 * 0.5 = 360 height
            // PadX = (640 - 640)/2 = 0
            // PadY = (640 - 360)/2 = 140
            Assert.Equal(0.0f, padX, 2);
            Assert.Equal(140.0f, padY, 2);

            // Tensor shape must be [1, 3, 640, 640]
            Assert.Equal(1, tensor.Dimensions[0]);
            Assert.Equal(3, tensor.Dimensions[1]);
            Assert.Equal(640, tensor.Dimensions[2]);
            Assert.Equal(640, tensor.Dimensions[3]);
        }

        [Fact]
        public void PreprocessImage_ValuesNormalizedBetweenZeroAndOne()
        {
            using var bmp = new Bitmap(100, 100);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255, 128, 64)); // Known non-zero color
            }

            var tensor = OnnxDetectionService.PreprocessImage(
                bmp,
                targetWidth: 640,
                targetHeight: 640,
                letterbox: true,
                out _, out _, out _);

            // Sample pixels from tensor
            float r = tensor[0, 0, 320, 320];
            float gVal = tensor[0, 1, 320, 320];
            float b = tensor[0, 2, 320, 320];

            Assert.InRange(r, 0.0f, 1.0f);
            Assert.InRange(gVal, 0.0f, 1.0f);
            Assert.InRange(b, 0.0f, 1.0f);
        }

        [Fact]
        public void PreprocessImage_DirectStretchMode_HasZeroPadsAndScaleOne()
        {
            using var bmp = new Bitmap(800, 600);

            var tensor = OnnxDetectionService.PreprocessImage(
                bmp,
                targetWidth: 640,
                targetHeight: 640,
                letterbox: false,
                out float scale,
                out float padX,
                out float padY);

            Assert.Equal(1.0f, scale);
            Assert.Equal(0.0f, padX);
            Assert.Equal(0.0f, padY);
        }
    }
}
