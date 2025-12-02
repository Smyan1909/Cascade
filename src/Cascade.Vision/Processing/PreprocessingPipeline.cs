namespace Cascade.Vision.Processing;

/// <summary>
/// Chainable image preprocessing pipeline for OCR optimization.
/// </summary>
public class PreprocessingPipeline
{
    private readonly List<Func<byte[], byte[]>> _steps = new();
    private readonly ImageProcessor _processor = new();

    /// <summary>
    /// Adds grayscale conversion to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddGrayscale()
    {
        _steps.Add(img => _processor.ToGrayscale(img));
        return this;
    }

    /// <summary>
    /// Adds resizing by scale factor to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddResize(int scaleFactor)
    {
        _steps.Add(img => _processor.Scale(img, scaleFactor));
        return this;
    }

    /// <summary>
    /// Adds resizing to specific dimensions.
    /// </summary>
    public PreprocessingPipeline AddResize(int width, int height, ResizeMode mode = ResizeMode.Fit)
    {
        _steps.Add(img => _processor.Resize(img, width, height, mode));
        return this;
    }

    /// <summary>
    /// Adds deskewing to the pipeline.
    /// Note: This is a simplified implementation that enhances edges.
    /// </summary>
    public PreprocessingPipeline AddDeskew()
    {
        _steps.Add(img =>
        {
            // Deskewing would require more complex edge detection and rotation
            // For now, we sharpen to enhance text edges
            return _processor.Sharpen(img, 1f);
        });
        return this;
    }

    /// <summary>
    /// Adds binary thresholding to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddBinarize(float threshold = 0.5f)
    {
        _steps.Add(img => _processor.Binarize(img, threshold));
        return this;
    }

    /// <summary>
    /// Adds denoising to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddDenoise(float sigma = 1f)
    {
        _steps.Add(img => _processor.Denoise(img, sigma));
        return this;
    }

    /// <summary>
    /// Adds sharpening to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddSharpen(float sigma = 3f)
    {
        _steps.Add(img => _processor.Sharpen(img, sigma));
        return this;
    }

    /// <summary>
    /// Adds contrast adjustment to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddContrastAdjustment(float contrast = 1.2f)
    {
        _steps.Add(img => _processor.AdjustContrast(img, contrast));
        return this;
    }

    /// <summary>
    /// Adds brightness adjustment to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddBrightnessAdjustment(float brightness = 1.0f)
    {
        _steps.Add(img => _processor.AdjustBrightness(img, brightness));
        return this;
    }

    /// <summary>
    /// Adds color inversion to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddInvert()
    {
        _steps.Add(img => _processor.Invert(img));
        return this;
    }

    /// <summary>
    /// Adds adaptive thresholding to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddAdaptiveThreshold()
    {
        _steps.Add(img => _processor.AdaptiveThreshold(img));
        return this;
    }

    /// <summary>
    /// Adds a custom processing step to the pipeline.
    /// </summary>
    public PreprocessingPipeline AddCustom(Func<byte[], byte[]> processor)
    {
        _steps.Add(processor);
        return this;
    }

    /// <summary>
    /// Processes an image through all steps in the pipeline.
    /// </summary>
    public byte[] Process(byte[] imageData)
    {
        var result = imageData;
        foreach (var step in _steps)
        {
            result = step(result);
        }
        return result;
    }

    /// <summary>
    /// Clears all steps from the pipeline.
    /// </summary>
    public PreprocessingPipeline Clear()
    {
        _steps.Clear();
        return this;
    }

    /// <summary>
    /// Gets the number of steps in the pipeline.
    /// </summary>
    public int StepCount => _steps.Count;

    #region Predefined Pipelines

    /// <summary>
    /// Creates a pipeline optimized for screen text (UI elements, dialogs).
    /// </summary>
    public static PreprocessingPipeline ForScreenText => new PreprocessingPipeline()
        .AddResize(2)
        .AddGrayscale()
        .AddContrastAdjustment(1.3f)
        .AddSharpen(1f);

    /// <summary>
    /// Creates a pipeline optimized for document text (scanned documents).
    /// </summary>
    public static PreprocessingPipeline ForDocument => new PreprocessingPipeline()
        .AddResize(2)
        .AddGrayscale()
        .AddDeskew()
        .AddAdaptiveThreshold();

    /// <summary>
    /// Creates a pipeline optimized for low contrast images.
    /// </summary>
    public static PreprocessingPipeline ForLowContrast => new PreprocessingPipeline()
        .AddResize(2)
        .AddGrayscale()
        .AddContrastAdjustment(1.5f)
        .AddBrightnessAdjustment(1.1f)
        .AddSharpen(2f);

    /// <summary>
    /// Creates a pipeline optimized for dark backgrounds with light text.
    /// </summary>
    public static PreprocessingPipeline ForDarkBackground => new PreprocessingPipeline()
        .AddResize(2)
        .AddGrayscale()
        .AddInvert()
        .AddContrastAdjustment(1.2f)
        .AddSharpen(1f);

    /// <summary>
    /// Creates a pipeline for general OCR with minimal processing.
    /// </summary>
    public static PreprocessingPipeline Minimal => new PreprocessingPipeline()
        .AddResize(2);

    /// <summary>
    /// Creates an empty pipeline (no processing).
    /// </summary>
    public static PreprocessingPipeline None => new PreprocessingPipeline();

    #endregion
}

