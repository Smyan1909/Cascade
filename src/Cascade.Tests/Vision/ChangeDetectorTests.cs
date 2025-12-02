using Cascade.Vision.Comparison;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace Cascade.Tests.Vision;

/// <summary>
/// Tests for the ChangeDetector class.
/// </summary>
public class ChangeDetectorTests
{
    private readonly ChangeDetector _detector = new();

    private static byte[] CreateSolidImage(int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height, color);
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static byte[] CreateImageWithRegion(int width, int height, Rgba32 background, Rgba32 regionColor, System.Drawing.Rectangle region)
    {
        using var image = new Image<Rgba32>(width, height, background);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = region.Top; y < region.Bottom && y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = region.Left; x < region.Right && x < row.Length; x++)
                {
                    row[x] = regionColor;
                }
            }
        });
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    [Fact]
    public async Task Compare_IdenticalImages_ReturnsNoChanges()
    {
        // Arrange
        var image = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));

        // Act
        var result = await _detector.CompareAsync(image, image);

        // Assert
        Assert.False(result.HasChanges);
        Assert.Equal(ChangeType.None, result.ChangeType);
        Assert.Equal(0, result.DifferencePercentage, 5);
    }

    [Fact]
    public async Task Compare_CompletelyDifferentImages_ReturnsCompleteChange()
    {
        // Arrange
        var baseline = CreateSolidImage(100, 100, new Rgba32(0, 0, 0, 255));
        var current = CreateSolidImage(100, 100, new Rgba32(255, 255, 255, 255));

        // Act
        var result = await _detector.CompareAsync(baseline, current);

        // Assert
        Assert.True(result.HasChanges);
        Assert.Equal(ChangeType.Complete, result.ChangeType);
        Assert.Equal(1.0, result.DifferencePercentage, 2);
    }

    [Fact]
    public async Task Compare_SmallChange_ReturnsMinorChange()
    {
        // Arrange
        var baseline = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));
        var current = CreateImageWithRegion(100, 100, 
            new Rgba32(128, 128, 128, 255), 
            new Rgba32(255, 0, 0, 255),
            new System.Drawing.Rectangle(45, 45, 10, 10)); // 1% of image

        // Act
        var result = await _detector.CompareAsync(baseline, current);

        // Assert
        Assert.True(result.HasChanges);
        Assert.True(result.DifferencePercentage < 0.05);
    }

    [Fact]
    public async Task Compare_WithColorTolerance_IgnoresSmallDifferences()
    {
        // Arrange
        var baseline = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));
        var current = CreateSolidImage(100, 100, new Rgba32(130, 130, 130, 255)); // Small difference

        _detector.Options.ColorTolerance = 10;

        // Act
        var result = await _detector.CompareAsync(baseline, current);

        // Assert
        Assert.False(result.HasChanges);
    }

    [Fact]
    public async Task SetBaseline_ThenCompare_Works()
    {
        // Arrange
        var baseline = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));
        var current = CreateSolidImage(100, 100, new Rgba32(255, 255, 255, 255));

        await _detector.SetBaselineAsync(baseline);

        // Act
        var result = await _detector.CompareWithBaselineAsync(current);

        // Assert
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void HasBaseline_AfterSet_ReturnsTrue()
    {
        // Arrange
        var image = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));

        // Act
        _detector.SetBaselineAsync(image).Wait();

        // Assert
        Assert.True(_detector.HasBaseline);
    }

    [Fact]
    public void ClearBaseline_RemovesBaseline()
    {
        // Arrange
        var image = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));
        _detector.SetBaselineAsync(image).Wait();

        // Act
        _detector.ClearBaseline();

        // Assert
        Assert.False(_detector.HasBaseline);
    }

    [Fact]
    public async Task CompareWithBaseline_WithoutBaseline_ThrowsException()
    {
        // Arrange
        var detector = new ChangeDetector();
        var current = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            detector.CompareWithBaselineAsync(current));
    }

    [Fact]
    public async Task Compare_GeneratesDifferenceImage()
    {
        // Arrange
        var baseline = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));
        var current = CreateImageWithRegion(100, 100,
            new Rgba32(128, 128, 128, 255),
            new Rgba32(255, 0, 0, 255),
            new System.Drawing.Rectangle(40, 40, 20, 20));

        _detector.Options.GenerateDifferenceImage = true;

        // Act
        var result = await _detector.CompareAsync(baseline, current);

        // Assert
        Assert.NotNull(result.DifferenceImage);
        Assert.True(result.DifferenceImage.Length > 0);
    }

    [Fact]
    public async Task Compare_WithIgnoreRegions_IgnoresThoseAreas()
    {
        // Arrange
        var baseline = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));
        var current = CreateImageWithRegion(100, 100,
            new Rgba32(128, 128, 128, 255),
            new Rgba32(255, 0, 0, 255),
            new System.Drawing.Rectangle(40, 40, 20, 20));

        _detector.Options.IgnoreRegions = new[]
        {
            new System.Drawing.Rectangle(35, 35, 30, 30) // Covers the changed area
        };

        // Act
        var result = await _detector.CompareAsync(baseline, current);

        // Assert
        Assert.False(result.HasChanges);
    }

    [Fact]
    public async Task Compare_ChangedPixelCount_IsCorrect()
    {
        // Arrange
        var baseline = CreateSolidImage(100, 100, new Rgba32(128, 128, 128, 255));
        var current = CreateImageWithRegion(100, 100,
            new Rgba32(128, 128, 128, 255),
            new Rgba32(255, 0, 0, 255),
            new System.Drawing.Rectangle(0, 0, 10, 10)); // 100 pixels changed

        _detector.Options.ColorTolerance = 0;

        // Act
        var result = await _detector.CompareAsync(baseline, current);

        // Assert
        Assert.Equal(100, result.ChangedPixelCount);
        Assert.Equal(10000, result.TotalPixelCount);
        Assert.Equal(0.01, result.DifferencePercentage, 5);
    }
}

