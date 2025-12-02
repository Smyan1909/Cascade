using Cascade.Vision.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace Cascade.Tests.Vision;

/// <summary>
/// Tests for the PreprocessingPipeline class.
/// </summary>
public class PreprocessingPipelineTests
{
    private static byte[] CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(128, 128, 128, 255));
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    [Fact]
    public void EmptyPipeline_Process_ReturnsOriginalData()
    {
        // Arrange
        var pipeline = new PreprocessingPipeline();
        var imageData = CreateTestImage(50, 50);

        // Act
        var result = pipeline.Process(imageData);

        // Assert - same dimensions
        var processor = new ImageProcessor();
        var (origWidth, origHeight) = processor.GetDimensions(imageData);
        var (newWidth, newHeight) = processor.GetDimensions(result);
        Assert.Equal(origWidth, newWidth);
        Assert.Equal(origHeight, newHeight);
    }

    [Fact]
    public void Pipeline_WithResize_DoublesSize()
    {
        // Arrange
        var pipeline = new PreprocessingPipeline()
            .AddResize(2);
        var imageData = CreateTestImage(50, 50);

        // Act
        var result = pipeline.Process(imageData);

        // Assert
        var processor = new ImageProcessor();
        var (width, height) = processor.GetDimensions(result);
        Assert.Equal(100, width);
        Assert.Equal(100, height);
    }

    [Fact]
    public void Pipeline_ChainedSteps_ExecutesInOrder()
    {
        // Arrange
        var pipeline = new PreprocessingPipeline()
            .AddGrayscale()
            .AddContrastAdjustment(1.2f)
            .AddResize(2);
        var imageData = CreateTestImage(50, 50);

        // Act
        var result = pipeline.Process(imageData);

        // Assert
        var processor = new ImageProcessor();
        var (width, height) = processor.GetDimensions(result);
        Assert.Equal(100, width);
        Assert.Equal(100, height);
    }

    [Fact]
    public void Pipeline_Clear_RemovesAllSteps()
    {
        // Arrange
        var pipeline = new PreprocessingPipeline()
            .AddGrayscale()
            .AddResize(2);

        Assert.Equal(2, pipeline.StepCount);

        // Act
        pipeline.Clear();

        // Assert
        Assert.Equal(0, pipeline.StepCount);
    }

    [Fact]
    public void Pipeline_AddCustom_ExecutesCustomFunction()
    {
        // Arrange
        bool customExecuted = false;
        var pipeline = new PreprocessingPipeline()
            .AddCustom(img =>
            {
                customExecuted = true;
                return img;
            });
        var imageData = CreateTestImage(50, 50);

        // Act
        var result = pipeline.Process(imageData);

        // Assert
        Assert.True(customExecuted);
    }

    [Fact]
    public void ForScreenText_HasCorrectSteps()
    {
        // Arrange
        var pipeline = PreprocessingPipeline.ForScreenText;

        // Assert - should have multiple steps
        Assert.True(pipeline.StepCount >= 3);
    }

    [Fact]
    public void ForDocument_HasCorrectSteps()
    {
        // Arrange
        var pipeline = PreprocessingPipeline.ForDocument;

        // Assert - should have multiple steps
        Assert.True(pipeline.StepCount >= 3);
    }

    [Fact]
    public void None_IsEmpty()
    {
        // Arrange
        var pipeline = PreprocessingPipeline.None;

        // Assert
        Assert.Equal(0, pipeline.StepCount);
    }

    [Fact]
    public void Pipeline_WithAllSteps_ProcessesWithoutError()
    {
        // Arrange
        var pipeline = new PreprocessingPipeline()
            .AddGrayscale()
            .AddResize(2)
            .AddContrastAdjustment(1.3f)
            .AddBrightnessAdjustment(1.1f)
            .AddSharpen(2f)
            .AddDenoise(1f);

        var imageData = CreateTestImage(50, 50);

        // Act & Assert - should not throw
        var result = pipeline.Process(imageData);
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }
}

