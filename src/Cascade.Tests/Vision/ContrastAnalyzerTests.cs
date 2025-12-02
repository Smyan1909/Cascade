using System.Drawing;
using Cascade.Vision.Analysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace Cascade.Tests.Vision;

/// <summary>
/// Tests for the ContrastAnalyzer class.
/// </summary>
public class ContrastAnalyzerTests
{
    private readonly ContrastAnalyzer _analyzer = new();

    private static byte[] CreateImageWithColors(Rgba32 topColor, Rgba32 bottomColor)
    {
        using var image = new Image<Rgba32>(100, 100);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var color = y < 50 ? topColor : bottomColor;
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = color;
                }
            }
        });
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    [Fact]
    public void CalculateContrastRatio_BlackOnWhite_Returns21()
    {
        // Arrange
        var foreground = System.Drawing.Color.Black;
        var background = System.Drawing.Color.White;

        // Act
        var ratio = _analyzer.CalculateContrastRatio(foreground, background);

        // Assert
        Assert.Equal(21.0, ratio, 0);
    }

    [Fact]
    public void CalculateContrastRatio_WhiteOnWhite_Returns1()
    {
        // Arrange
        var foreground = System.Drawing.Color.White;
        var background = System.Drawing.Color.White;

        // Act
        var ratio = _analyzer.CalculateContrastRatio(foreground, background);

        // Assert
        Assert.Equal(1.0, ratio, 0);
    }

    [Fact]
    public void CalculateContrastRatio_GrayOnWhite_ReturnsMediumValue()
    {
        // Arrange
        var foreground = System.Drawing.Color.Gray;
        var background = System.Drawing.Color.White;

        // Act
        var ratio = _analyzer.CalculateContrastRatio(foreground, background);

        // Assert
        Assert.True(ratio > 1 && ratio < 21);
    }

    [Fact]
    public void MeetsWcagAA_BlackOnWhite_ReturnsTrue()
    {
        // Arrange
        var foreground = System.Drawing.Color.Black;
        var background = System.Drawing.Color.White;

        // Act
        var meets = _analyzer.MeetsWcagAA(foreground, background);

        // Assert
        Assert.True(meets);
    }

    [Fact]
    public void MeetsWcagAA_LightGrayOnWhite_ReturnsFalse()
    {
        // Arrange
        var foreground = System.Drawing.Color.FromArgb(200, 200, 200);
        var background = System.Drawing.Color.White;

        // Act
        var meets = _analyzer.MeetsWcagAA(foreground, background);

        // Assert
        Assert.False(meets);
    }

    [Fact]
    public void MeetsWcagAAA_BlackOnWhite_ReturnsTrue()
    {
        // Arrange
        var foreground = System.Drawing.Color.Black;
        var background = System.Drawing.Color.White;

        // Act
        var meets = _analyzer.MeetsWcagAAA(foreground, background);

        // Assert
        Assert.True(meets);
    }

    [Fact]
    public void GetContrastingColor_LightBackground_ReturnsBlack()
    {
        // Arrange
        var background = System.Drawing.Color.White;

        // Act
        var contrasting = _analyzer.GetContrastingColor(background);

        // Assert
        Assert.Equal(System.Drawing.Color.Black.ToArgb(), contrasting.ToArgb());
    }

    [Fact]
    public void GetContrastingColor_DarkBackground_ReturnsWhite()
    {
        // Arrange
        var background = System.Drawing.Color.Black;

        // Act
        var contrasting = _analyzer.GetContrastingColor(background);

        // Assert
        Assert.Equal(System.Drawing.Color.White.ToArgb(), contrasting.ToArgb());
    }

    [Fact]
    public void AnalyzeContrast_HighContrastImage_ReturnsSuitableForOcr()
    {
        // Arrange
        var imageData = CreateImageWithColors(
            new Rgba32(0, 0, 0, 255),     // Black
            new Rgba32(255, 255, 255, 255) // White
        );

        // Act
        var result = _analyzer.AnalyzeContrast(imageData);

        // Assert
        Assert.True(result.IsAnalyzable);
        Assert.True(result.IsSuitableForOcr);
        Assert.True(result.ContrastRatio > 3.0);
    }

    [Fact]
    public void AnalyzeContrast_LowContrastImage_ReturnsNotSuitableForOcr()
    {
        // Arrange
        var imageData = CreateImageWithColors(
            new Rgba32(128, 128, 128, 255), // Gray
            new Rgba32(150, 150, 150, 255)  // Similar gray
        );

        // Act
        var result = _analyzer.AnalyzeContrast(imageData);

        // Assert
        Assert.True(result.IsAnalyzable);
        Assert.False(result.IsSuitableForOcr);
        Assert.True(result.ContrastRatio < 3.0);
    }

    [Fact]
    public void SuggestImprovedColors_LowContrast_ReturnsHigherContrast()
    {
        // Arrange
        var foreground = System.Drawing.Color.FromArgb(150, 150, 150);
        var background = System.Drawing.Color.White;
        var originalRatio = _analyzer.CalculateContrastRatio(foreground, background);

        // Act
        var (newFg, newBg) = _analyzer.SuggestImprovedColors(foreground, background, 4.5);

        // Assert
        var newRatio = _analyzer.CalculateContrastRatio(newFg, newBg);
        Assert.True(newRatio >= originalRatio);
    }

    [Fact]
    public void SuggestImprovedColors_AlreadyGood_ReturnsSameColors()
    {
        // Arrange
        var foreground = System.Drawing.Color.Black;
        var background = System.Drawing.Color.White;

        // Act
        var (newFg, newBg) = _analyzer.SuggestImprovedColors(foreground, background, 4.5);

        // Assert
        Assert.Equal(foreground.ToArgb(), newFg.ToArgb());
        Assert.Equal(background.ToArgb(), newBg.ToArgb());
    }

    [Fact]
    public void AnalyzeContrast_Region_AnalyzesCorrectRegion()
    {
        // Arrange
        // Create image with different colors in different regions
        using var image = new Image<Rgba32>(100, 100);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    // Top-left: black and white (high contrast)
                    // Bottom-right: gray on gray (low contrast)
                    if (x < 50 && y < 50)
                    {
                        row[x] = y < 25 ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
                    }
                    else
                    {
                        row[x] = new Rgba32(128, 128, 128, 255);
                    }
                }
            }
        });
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        var imageData = ms.ToArray();

        // Act
        var highContrastRegion = _analyzer.AnalyzeContrast(
            imageData, 
            new System.Drawing.Rectangle(0, 0, 50, 50));
        
        var lowContrastRegion = _analyzer.AnalyzeContrast(
            imageData,
            new System.Drawing.Rectangle(50, 50, 50, 50));

        // Assert
        Assert.True(highContrastRegion.ContrastRatio > lowContrastRegion.ContrastRatio);
    }
}

