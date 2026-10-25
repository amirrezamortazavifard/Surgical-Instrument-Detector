using SurgicalDetector.Services;
using System.Drawing;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class IoUCalculationTests
    {
        [Fact]
        public void CalculateIoU_IdenticalBoxes_ReturnsOne()
        {
            var boxA = new Rectangle(100, 100, 50, 50);
            var boxB = new Rectangle(100, 100, 50, 50);

            float iou = OnnxDetectionService.CalculateIoU(boxA, boxB);

            Assert.Equal(1.0f, iou, 3);
        }

        [Fact]
        public void CalculateIoU_CompletelyDisjointBoxes_ReturnsZero()
        {
            var boxA = new Rectangle(0, 0, 50, 50);
            var boxB = new Rectangle(100, 100, 50, 50);

            float iou = OnnxDetectionService.CalculateIoU(boxA, boxB);

            Assert.Equal(0.0f, iou);
        }

        [Fact]
        public void CalculateIoU_KnownHalfOverlap_ReturnsAccurateRatio()
        {
            // Box A: (0, 0) to (100, 100), Area = 10,000
            // Box B: (50, 0) to (150, 100), Area = 10,000
            // Intersection: (50, 0) to (100, 100), Width = 50, Height = 100, Area = 5,000
            // Union: 10,000 + 10,000 - 5,000 = 15,000
            // Expected IoU = 5,000 / 15,000 = 1/3 ~ 0.3333f
            var boxA = new Rectangle(0, 0, 100, 100);
            var boxB = new Rectangle(50, 0, 100, 100);

            float iou = OnnxDetectionService.CalculateIoU(boxA, boxB);

            Assert.Equal(1.0f / 3.0f, iou, 3);
        }

        [Fact]
        public void CalculateIoU_OneContainedInsideAnother_CalculatesCorrectly()
        {
            // Outer Box: 100x100, Area = 10,000
            // Inner Box: 50x50, Area = 2,500
            // Intersection: 2,500
            // Union: 10,000
            // IoU: 2,500 / 10,000 = 0.25f
            var boxOuter = new Rectangle(0, 0, 100, 100);
            var boxInner = new Rectangle(25, 25, 50, 50);

            float iou = OnnxDetectionService.CalculateIoU(boxOuter, boxInner);

            Assert.Equal(0.25f, iou, 3);
        }
    }
}
