using System.Diagnostics;
using System.Drawing;
using Cascade.Vision.Capture;

namespace Cascade.Vision.OCR;

/// <summary>
/// Composite OCR engine that intelligently orchestrates multiple OCR engines.
/// Uses fast engines first and falls back to more powerful engines when needed.
/// </summary>
public class CompositeOcrEngine : IOcrEngine, IDisposable
{
    private readonly WindowsOcrEngine _windowsOcr;
    private readonly TesseractOcrEngine _tesseract;
    private readonly PaddleOcrEngine _paddleOcr;
    private readonly List<IOcrEngine> _engines;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the minimum confidence threshold to accept a result.
    /// Results below this will trigger fallback to the next engine.
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the minimum text length to consider a result valid.
    /// </summary>
    public int MinTextLength { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to enable automatic fallback to PaddleOCR.
    /// </summary>
    public bool EnablePaddleFallback { get; set; } = true;

    /// <inheritdoc />
    public string EngineName => "Composite";

    /// <inheritdoc />
    public OcrOptions Options { get; set; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedLanguages
    {
        get
        {
            var languages = new HashSet<string>();
            foreach (var engine in _engines.Where(e => e.IsAvailable))
            {
                foreach (var lang in engine.SupportedLanguages)
                    languages.Add(lang);
            }
            return languages.ToList();
        }
    }

    /// <inheritdoc />
    public bool IsAvailable => _engines.Any(e => e.IsAvailable);

    /// <inheritdoc />
    public int Priority => 0; // Highest priority as it orchestrates others

    /// <summary>
    /// Creates a new CompositeOcrEngine with default engine configuration.
    /// </summary>
    public CompositeOcrEngine()
        : this(new WindowsOcrEngine(), new TesseractOcrEngine(), new PaddleOcrEngine())
    {
    }

    /// <summary>
    /// Creates a new CompositeOcrEngine with custom engines.
    /// </summary>
    public CompositeOcrEngine(WindowsOcrEngine windowsOcr, TesseractOcrEngine tesseract, PaddleOcrEngine paddleOcr)
    {
        _windowsOcr = windowsOcr;
        _tesseract = tesseract;
        _paddleOcr = paddleOcr;

        _engines = new List<IOcrEngine> { _windowsOcr, _tesseract, _paddleOcr };
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        return await RecognizeWithStrategyAsync(imageData, Options.EnginePreference, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await RecognizeAsync(imageData, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default)
    {
        return RecognizeAsync(capture.ImageData, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default)
    {
        var engines = GetEnginesForPreference(Options.EnginePreference);
        
        foreach (var engine in engines)
        {
            if (!engine.IsAvailable)
                continue;

            try
            {
                engine.Options = Options;
                var result = await engine.RecognizeRegionAsync(imageData, region, cancellationToken);
                
                if (IsResultAcceptable(result))
                    return result;
            }
            catch
            {
                // Continue to next engine
            }
        }

        return OcrResult.Empty(EngineName);
    }

    /// <summary>
    /// Recognizes text and searches for a specific target text.
    /// Uses PaddleOCR fallback if the target text is not found by faster engines.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="targetText">The text to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCR result.</returns>
    public async Task<OcrResult> RecognizeWithTargetAsync(
        byte[] imageData, 
        string targetText, 
        CancellationToken cancellationToken = default)
    {
        return await RecognizeWithStrategyAsync(imageData, Options.EnginePreference, targetText, cancellationToken);
    }

    private async Task<OcrResult> RecognizeWithStrategyAsync(
        byte[] imageData,
        OcrEnginePreference preference,
        string? targetText,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        switch (preference)
        {
            case OcrEnginePreference.WindowsOnly:
                return await TryEngineAsync(_windowsOcr, imageData, cancellationToken);

            case OcrEnginePreference.TesseractOnly:
                return await TryEngineAsync(_tesseract, imageData, cancellationToken);

            case OcrEnginePreference.PaddleOcrOnly:
                return await TryEngineAsync(_paddleOcr, imageData, cancellationToken);

            case OcrEnginePreference.BestConfidence:
                return await GetBestConfidenceResultAsync(imageData, cancellationToken);

            case OcrEnginePreference.SmartFallback:
                return await SmartFallbackAsync(imageData, targetText, cancellationToken);

            case OcrEnginePreference.TesseractFirst:
                return await FallbackChainAsync(
                    imageData, 
                    new IOcrEngine[] { _tesseract, _windowsOcr, _paddleOcr },
                    targetText,
                    cancellationToken);

            case OcrEnginePreference.WindowsFirst:
            default:
                return await FallbackChainAsync(
                    imageData,
                    new IOcrEngine[] { _windowsOcr, _tesseract, _paddleOcr },
                    targetText,
                    cancellationToken);
        }
    }

    private async Task<OcrResult> TryEngineAsync(IOcrEngine engine, byte[] imageData, CancellationToken cancellationToken)
    {
        if (!engine.IsAvailable)
            return OcrResult.Empty(engine.EngineName);

        try
        {
            engine.Options = Options;
            return await engine.RecognizeAsync(imageData, cancellationToken);
        }
        catch
        {
            return OcrResult.Empty(engine.EngineName);
        }
    }

    private async Task<OcrResult> FallbackChainAsync(
        byte[] imageData,
        IOcrEngine[] engines,
        string? targetText,
        CancellationToken cancellationToken)
    {
        OcrResult? bestResult = null;

        foreach (var engine in engines)
        {
            if (!engine.IsAvailable)
                continue;

            // Skip PaddleOCR unless fallback is enabled
            if (engine == _paddleOcr && !EnablePaddleFallback)
                continue;

            try
            {
                engine.Options = Options;
                var result = await engine.RecognizeAsync(imageData, cancellationToken);

                // If we have a target text and found it, return immediately
                if (!string.IsNullOrEmpty(targetText) && result.ContainsText(targetText))
                    return result;

                // If result is acceptable, return it
                if (IsResultAcceptable(result))
                    return result;

                // Keep track of best result so far
                if (bestResult == null || result.Confidence > bestResult.Confidence)
                    bestResult = result;
            }
            catch
            {
                // Continue to next engine
            }
        }

        return bestResult ?? OcrResult.Empty(EngineName);
    }

    private async Task<OcrResult> SmartFallbackAsync(
        byte[] imageData,
        string? targetText,
        CancellationToken cancellationToken)
    {
        // Try Windows OCR first (fastest)
        if (_windowsOcr.IsAvailable)
        {
            try
            {
                _windowsOcr.Options = Options;
                var windowsResult = await _windowsOcr.RecognizeAsync(imageData, cancellationToken);

                // Check if target text found
                if (!string.IsNullOrEmpty(targetText) && windowsResult.ContainsText(targetText))
                    return windowsResult;

                // Check if result is good enough
                if (IsResultAcceptable(windowsResult))
                    return windowsResult;
            }
            catch
            {
                // Continue to Tesseract
            }
        }

        // Try Tesseract (still fast, more configurable)
        if (_tesseract.IsAvailable)
        {
            try
            {
                _tesseract.Options = Options;
                var tesseractResult = await _tesseract.RecognizeAsync(imageData, cancellationToken);

                // Check if target text found
                if (!string.IsNullOrEmpty(targetText) && tesseractResult.ContainsText(targetText))
                    return tesseractResult;

                // Check if result is good enough
                if (IsResultAcceptable(tesseractResult))
                    return tesseractResult;
            }
            catch
            {
                // Continue to PaddleOCR
            }
        }

        // Fallback to PaddleOCR (slowest but most powerful)
        if (EnablePaddleFallback && _paddleOcr.IsAvailable)
        {
            try
            {
                _paddleOcr.Options = Options;
                return await _paddleOcr.RecognizeAsync(imageData, cancellationToken);
            }
            catch
            {
                // Return empty result
            }
        }

        return OcrResult.Empty(EngineName);
    }

    private async Task<OcrResult> GetBestConfidenceResultAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        var tasks = new List<Task<OcrResult>>();
        var availableEngines = _engines.Where(e => e.IsAvailable).ToList();

        foreach (var engine in availableEngines)
        {
            engine.Options = Options;
            tasks.Add(engine.RecognizeAsync(imageData, cancellationToken));
        }

        if (tasks.Count == 0)
            return OcrResult.Empty(EngineName);

        var results = await Task.WhenAll(tasks);
        
        // Return result with highest confidence
        return results
            .Where(r => r.HasText)
            .OrderByDescending(r => r.Confidence)
            .FirstOrDefault() ?? OcrResult.Empty(EngineName);
    }

    private bool IsResultAcceptable(OcrResult result)
    {
        // Check confidence threshold
        if (result.Confidence < ConfidenceThreshold)
            return false;

        // Check minimum text length
        if (string.IsNullOrWhiteSpace(result.FullText) || 
            result.FullText.Length < MinTextLength)
            return false;

        return true;
    }

    private IOcrEngine[] GetEnginesForPreference(OcrEnginePreference preference)
    {
        return preference switch
        {
            OcrEnginePreference.WindowsOnly => new IOcrEngine[] { _windowsOcr },
            OcrEnginePreference.TesseractOnly => new IOcrEngine[] { _tesseract },
            OcrEnginePreference.PaddleOcrOnly => new IOcrEngine[] { _paddleOcr },
            OcrEnginePreference.TesseractFirst => new IOcrEngine[] { _tesseract, _windowsOcr, _paddleOcr },
            _ => new IOcrEngine[] { _windowsOcr, _tesseract, _paddleOcr }
        };
    }

    /// <summary>
    /// Gets the status of all engines.
    /// </summary>
    public IReadOnlyDictionary<string, bool> GetEngineStatus()
    {
        return new Dictionary<string, bool>
        {
            [_windowsOcr.EngineName] = _windowsOcr.IsAvailable,
            [_tesseract.EngineName] = _tesseract.IsAvailable,
            [_paddleOcr.EngineName] = _paddleOcr.IsAvailable
        };
    }

    /// <summary>
    /// Disposes of all engines.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _tesseract.Dispose();
        _paddleOcr.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}

