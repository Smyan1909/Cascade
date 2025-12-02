using System.Drawing;
using Cascade.Vision.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;
using Image = SixLabors.ImageSharp.Image;

namespace Cascade.Tests.Vision;

/// <summary>
/// Tests for the ImageProcessor class.
/// </summary>
public class ImageProcessorTests
{
    private readonly ImageProcessor _processor = new();

    private static byte[] CreateTestImage(int width, int height, Rgba32? color = null)
    {
        using var image = new Image<Rgba32>(width, height, color ?? new Rgba32(128, 128, 128, 255));
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static byte[] CreateGradientImage(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    byte val = (byte)(x * 255 / width);
                    row[x] = new Rgba32(val, val, val, 255);
                }
            }
        });
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    [Fact]
    public void Crop_ValidRegion_ReturnsCroppedImage()
    {
        // Arrange
        var imageData = CreateTestImage(100, 100);
        var region = new System.Drawing.Rectangle(10, 10, 50, 50);

        // Act
        var result = _processor.Crop(imageData, region);

        // Assert
        var (width, height) = _processor.GetDimensions(result);
        Assert.Equal(50, width);
        Assert.Equal(50, height);
    }

    [Fact]
    public void Scale_Factor2_DoublesSize()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50);

        // Act
        var result = _processor.Scale(imageData, 2.0);

        // Assert
        var (width, height) = _processor.GetDimensions(result);
        Assert.Equal(100, width);
        Assert.Equal(100, height);
    }

    [Fact]
    public void Resize_SpecificDimensions_ResizesCorrectly()
    {
        // Arrange
        var imageData = CreateTestImage(100, 100);

        // Act
        var result = _processor.Resize(imageData, 200, 150, ResizeMode.Stretch);

        // Assert
        var (width, height) = _processor.GetDimensions(result);
        Assert.Equal(200, width);
        Assert.Equal(150, height);
    }

    [Fact]
    public void ToGrayscale_ColorImage_ReturnsGrayscaleImage()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50, new Rgba32(255, 0, 0, 255)); // Red

        // Act
        var result = _processor.ToGrayscale(imageData);

        // Assert
        using var image = Image.Load<Rgba32>(result);
        var pixel = image[25, 25];
        // In grayscale, R=G=B
        Assert.Equal(pixel.R, pixel.G);
        Assert.Equal(pixel.G, pixel.B);
    }

    [Fact]
    public void Invert_WhiteImage_ReturnsBlackImage()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50, new Rgba32(255, 255, 255, 255));

        // Act
        var result = _processor.Invert(imageData);

        // Assert
        using var image = Image.Load<Rgba32>(result);
        var pixel = image[25, 25];
        Assert.Equal(0, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
    }

    [Fact]
    public void GetDominantColor_SolidColor_ReturnsThatColor()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50, new Rgba32(100, 150, 200, 255));

        // Act
        var result = _processor.GetDominantColor(imageData);

        // Assert
        Assert.Equal(100, result.R);
        Assert.Equal(150, result.G);
        Assert.Equal(200, result.B);
    }

    [Fact]
    public void GetBrightness_WhiteImage_ReturnsNearOne()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50, new Rgba32(255, 255, 255, 255));

        // Act
        var result = _processor.GetBrightness(imageData);

        // Assert
        Assert.True(result > 0.99);
    }

    [Fact]
    public void GetBrightness_BlackImage_ReturnsNearZero()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50, new Rgba32(0, 0, 0, 255));

        // Act
        var result = _processor.GetBrightness(imageData);

        // Assert
        Assert.True(result < 0.01);
    }

    [Fact]
    public void GetContrast_SolidColor_ReturnsZero()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50, new Rgba32(128, 128, 128, 255));

        // Act
        var result = _processor.GetContrast(imageData);

        // Assert
        Assert.Equal(0, result, 5);
    }

    [Fact]
    public void GetContrast_GradientImage_ReturnsPositiveValue()
    {
        // Arrange
        var imageData = CreateGradientImage(100, 100);

        // Act
        var result = _processor.GetContrast(imageData);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public void Binarize_GrayImage_ReturnsBinaryImage()
    {
        // Arrange
        var imageData = CreateGradientImage(100, 100);

        // Act
        var result = _processor.Binarize(imageData, 0.5f);

        // Assert
        using var image = Image.Load<Rgba32>(result);
        // Check that pixels are either black or white
        var centerPixel = image[50, 50];
        Assert.True(
            (centerPixel.R == 0 && centerPixel.G == 0 && centerPixel.B == 0) ||
            (centerPixel.R == 255 && centerPixel.G == 255 && centerPixel.B == 255));
    }

    [Fact]
    public void EnhanceForOcr_SmallImage_ReturnsLargerImage()
    {
        // Arrange
        var imageData = CreateTestImage(50, 50);

        // Act
        var result = _processor.EnhanceForOcr(imageData);

        // Assert
        var (width, height) = _processor.GetDimensions(result);
        Assert.True(width > 50);
        Assert.True(height > 50);
    }
}

