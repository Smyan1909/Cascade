using System.Drawing;
using Cascade.Vision.Analysis;
using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using Cascade.Vision.OCR;
using Cascade.Vision.Processing;

namespace Cascade.Vision.Services;

/// <summary>
/// Main service facade providing unified access to all vision capabilities.
/// </summary>
public class VisionService : IDisposable
{
    private readonly ScreenCapture _capture;
    private readonly CompositeOcrEngine _ocrEngine;
    private readonly ChangeDetector _changeDetector;
    private readonly ElementAnalyzer _elementAnalyzer;
    private readonly ContrastAnalyzer _contrastAnalyzer;
    private readonly ImageProcessor _imageProcessor;
    private bool _disposed;

    /// <summary>
    /// Gets the vision service options.
    /// </summary>
    public VisionOptions Options { get; }

    /// <summary>
    /// Creates a new VisionService with default options.
    /// </summary>
    public VisionService() : this(VisionOptions.Default)
    {
    }

    /// <summary>
    /// Creates a new VisionService with custom options.
    /// </summary>
    /// <param name="options">The vision options.</param>
    public VisionService(VisionOptions options)
    {
        Options = options;

        // Initialize capture
        _capture = new ScreenCapture { Options = options.DefaultCaptureOptions };

        // Initialize OCR engines
        var windowsOcr = new WindowsOcrEngine { Options = options.DefaultOcrOptions };
        var tesseract = new TesseractOcrEngine 
        { 
            Options = options.DefaultOcrOptions,
            TessDataPath = options.TesseractDataPath ?? "./tessdata"
        };
        var paddleOcr = new PaddleOcrEngine
        {
            Options = options.DefaultOcrOptions,
            ServiceEndpoint = options.PaddleOcr.ServiceEndpoint,
            Timeout = options.PaddleOcr.RequestTimeout,
            EnableRetry = options.PaddleOcr.EnableRetry,
            MaxRetries = options.PaddleOcr.MaxRetries,
            Model = options.PaddleOcr.DefaultModel,
            UseAngleClassifier = options.PaddleOcr.UseAngleClassifier
        };

        _ocrEngine = new CompositeOcrEngine(windowsOcr, tesseract, paddleOcr)
        {
            Options = options.DefaultOcrOptions,
            ConfidenceThreshold = options.PaddleOcr.FallbackConfidenceThreshold,
            EnablePaddleFallback = options.PaddleOcr.EnableAutoFallback
        };

        // Initialize other components
        _changeDetector = new ChangeDetector { Options = options.DefaultComparisonOptions };
        _elementAnalyzer = new ElementAnalyzer();
        _contrastAnalyzer = new ContrastAnalyzer();
        _imageProcessor = new ImageProcessor();
    }

    #region Screen Capture

    /// <summary>
    /// Captures the primary screen.
    /// </summary>
    public Task<CaptureResult> CaptureScreenAsync(CancellationToken cancellationToken = default)
    {
        return _capture.CapturePrimaryScreenAsync(cancellationToken);
    }

    /// <summary>
    /// Captures a specific screen by index.
    /// </summary>
    public Task<CaptureResult> CaptureScreenAsync(int screenIndex, CancellationToken cancellationToken = default)
    {
        return _capture.CaptureScreenAsync(screenIndex, cancellationToken);
    }

    /// <summary>
    /// Captures the foreground window.
    /// </summary>
    public Task<CaptureResult> CaptureForegroundWindowAsync(CancellationToken cancellationToken = default)
    {
        return _capture.CaptureForegroundWindowAsync(cancellationToken);
    }

    /// <summary>
    /// Captures a window by title.
    /// </summary>
    public Task<CaptureResult> CaptureWindowAsync(string windowTitle, CancellationToken cancellationToken = default)
    {
        return _capture.CaptureWindowAsync(windowTitle, false, cancellationToken);
    }

    /// <summary>
    /// Captures a specific region.
    /// </summary>
    public Task<CaptureResult> CaptureRegionAsync(Rectangle region, CancellationToken cancellationToken = default)
    {
        return _capture.CaptureRegionAsync(region, cancellationToken);
    }

    #endregion

    #region OCR

    /// <summary>
    /// Recognizes text in an image.
    /// </summary>
    public Task<OcrResult> RecognizeTextAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        return _ocrEngine.RecognizeAsync(imageData, cancellationToken);
    }

    /// <summary>
    /// Recognizes text in a capture result.
    /// </summary>
    public Task<OcrResult> RecognizeTextAsync(CaptureResult capture, CancellationToken cancellationToken = default)
    {
        return _ocrEngine.RecognizeAsync(capture, cancellationToken);
    }

    /// <summary>
    /// Recognizes text and searches for a specific target.
    /// Uses PaddleOCR fallback if target is not found by faster engines.
    /// </summary>
    public Task<OcrResult> RecognizeWithTargetAsync(
        byte[] imageData, 
        string targetText, 
        CancellationToken cancellationToken = default)
    {
        return _ocrEngine.RecognizeWithTargetAsync(imageData, targetText, cancellationToken);
    }

    /// <summary>
    /// Captures the foreground window and recognizes text.
    /// </summary>
    public async Task<OcrResult> CaptureAndRecognizeAsync(CancellationToken cancellationToken = default)
    {
        var capture = await CaptureForegroundWindowAsync(cancellationToken);
        return await RecognizeTextAsync(capture, cancellationToken);
    }

    /// <summary>
    /// Captures a window and recognizes text.
    /// </summary>
    public async Task<OcrResult> CaptureAndRecognizeAsync(string windowTitle, CancellationToken cancellationToken = default)
    {
        var capture = await CaptureWindowAsync(windowTitle, cancellationToken);
        return await RecognizeTextAsync(capture, cancellationToken);
    }

    /// <summary>
    /// Gets the status of all OCR engines.
    /// </summary>
    public IReadOnlyDictionary<string, bool> GetOcrEngineStatus()
    {
        return _ocrEngine.GetEngineStatus();
    }

    #endregion

    #region Change Detection

    /// <summary>
    /// Compares two images and returns the differences.
    /// </summary>
    public Task<ChangeResult> CompareImagesAsync(
        byte[] baseline, 
        byte[] current, 
        CancellationToken cancellationToken = default)
    {
        return _changeDetector.CompareAsync(baseline, current, cancellationToken);
    }

    /// <summary>
    /// Sets the baseline for subsequent comparisons.
    /// </summary>
    public Task SetBaselineAsync(byte[] imageData)
    {
        return _changeDetector.SetBaselineAsync(imageData);
    }

    /// <summary>
    /// Sets the baseline from a capture.
    /// </summary>
    public Task SetBaselineAsync(CaptureResult capture)
    {
        return _changeDetector.SetBaselineAsync(capture);
    }

    /// <summary>
    /// Compares with the stored baseline.
    /// </summary>
    public Task<ChangeResult> CompareWithBaselineAsync(
        byte[] current, 
        CancellationToken cancellationToken = default)
    {
        return _changeDetector.CompareWithBaselineAsync(current, cancellationToken);
    }

    /// <summary>
    /// Waits for a visual change in a region.
    /// </summary>
    public Task<ChangeResult> WaitForChangeAsync(
        Rectangle region, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default)
    {
        return _changeDetector.WaitForChangeAsync(_capture, region, timeout, cancellationToken);
    }

    /// <summary>
    /// Waits for the UI to stabilize (no changes for the specified duration).
    /// </summary>
    public Task<ChangeResult> WaitForStabilityAsync(
        Rectangle region,
        TimeSpan stabilityDuration,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return _changeDetector.WaitForStabilityAsync(
            _capture, region, stabilityDuration, timeout, cancellationToken);
    }

    #endregion

    #region Visual Analysis

    /// <summary>
    /// Detects visual elements in an image.
    /// </summary>
    public Task<IReadOnlyList<VisualElement>> DetectElementsAsync(
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        return _elementAnalyzer.DetectElementsAsync(imageData, cancellationToken);
    }

    /// <summary>
    /// Analyzes the layout of a UI.
    /// </summary>
    public Task<LayoutAnalysis> AnalyzeLayoutAsync(
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        return _elementAnalyzer.AnalyzeLayoutAsync(imageData, cancellationToken);
    }

    /// <summary>
    /// Analyzes contrast in an image region.
    /// </summary>
    public ContrastAnalysisResult AnalyzeContrast(byte[] imageData, Rectangle? region = null)
    {
        return _contrastAnalyzer.AnalyzeContrast(imageData, region);
    }

    #endregion

    #region Image Processing

    /// <summary>
    /// Gets the image processor for custom operations.
    /// </summary>
    public ImageProcessor ImageProcessor => _imageProcessor;

    /// <summary>
    /// Preprocesses an image for OCR.
    /// </summary>
    public byte[] PreprocessForOcr(byte[] imageData)
    {
        return _imageProcessor.EnhanceForOcr(imageData);
    }

    /// <summary>
    /// Creates a preprocessing pipeline.
    /// </summary>
    public PreprocessingPipeline CreatePipeline()
    {
        return new PreprocessingPipeline();
    }

    #endregion

    /// <summary>
    /// Disposes of all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _ocrEngine.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}

