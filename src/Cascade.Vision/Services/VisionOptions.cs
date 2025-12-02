using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using Cascade.Vision.OCR;

namespace Cascade.Vision.Services;

/// <summary>
/// Global configuration options for the Vision service.
/// </summary>
public class VisionOptions
{
    /// <summary>
    /// Gets or sets the default capture options.
    /// </summary>
    public CaptureOptions DefaultCaptureOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the default OCR options.
    /// </summary>
    public OcrOptions DefaultOcrOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the path to Tesseract data files.
    /// </summary>
    public string? TesseractDataPath { get; set; } = "./tessdata";

    /// <summary>
    /// Gets or sets the PaddleOCR service options.
    /// </summary>
    public PaddleOcrServiceOptions PaddleOcr { get; set; } = new();

    /// <summary>
    /// Gets or sets the default comparison options.
    /// </summary>
    public ComparisonOptions DefaultComparisonOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to enable screenshot caching.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of cached screenshots.
    /// </summary>
    public int MaxCachedScreenshots { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to save debug images.
    /// </summary>
    public bool SaveDebugImages { get; set; } = false;

    /// <summary>
    /// Gets or sets the path for saving debug images.
    /// </summary>
    public string? DebugImagePath { get; set; }

    /// <summary>
    /// Creates default vision options.
    /// </summary>
    public static VisionOptions Default => new();
}

/// <summary>
/// Configuration options for the PaddleOCR gRPC service connection.
/// </summary>
public class PaddleOcrServiceOptions
{
    /// <summary>
    /// Gets or sets the gRPC service endpoint.
    /// </summary>
    public string ServiceEndpoint { get; set; } = "http://localhost:50052";

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to enable retry on failure.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retries.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the default model to use.
    /// </summary>
    public PaddleOcrModel DefaultModel { get; set; } = PaddleOcrModel.PPOCRv4;

    /// <summary>
    /// Gets or sets the default language.
    /// </summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>
    /// Gets or sets whether to use angle classification.
    /// </summary>
    public bool UseAngleClassifier { get; set; } = true;

    /// <summary>
    /// Gets or sets the confidence threshold for triggering PaddleOCR fallback.
    /// </summary>
    public double FallbackConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets whether to enable automatic fallback to PaddleOCR.
    /// </summary>
    public bool EnableAutoFallback { get; set; } = true;
}

