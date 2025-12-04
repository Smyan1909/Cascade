using System.Linq;
using Cascade.Vision.Capture;

namespace Cascade.Vision.OCR;

public sealed class CompositeOcrEngine : IOcrEngine
{
    private readonly IOcrEngine _windows;
    private readonly IOcrEngine _tesseract;
    private readonly IOcrEngine _paddle;

    public CompositeOcrEngine(
        IOcrEngine windows,
        IOcrEngine tesseract,
        IOcrEngine paddle,
        OcrOptions? options = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _tesseract = tesseract ?? throw new ArgumentNullException(nameof(tesseract));
        _paddle = paddle ?? throw new ArgumentNullException(nameof(paddle));
        Options = options ?? new OcrOptions();
    }

    public double ConfidenceThreshold { get; set; } = 0.7;
    public double MinTextLengthForFallback { get; set; } = 3;

    public string EngineName => "Composite";
    public IReadOnlyList<string> SupportedLanguages => _windows.SupportedLanguages.Concat(_tesseract.SupportedLanguages).Distinct().ToList();
    public bool IsAvailable => _windows.IsAvailable || _tesseract.IsAvailable || _paddle.IsAvailable;

    public OcrOptions Options
    {
        get => _windows.Options;
        set
        {
            _windows.Options = value;
            _tesseract.Options = value;
            _paddle.Options = value;
        }
    }

    public Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default)
        => RecognizeAsync(capture.ImageData, cancellationToken);

    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_windows.IsAvailable)
        {
            var result = await _windows.RecognizeAsync(imageData, cancellationToken);
            if (IsResultAcceptable(result))
            {
                return result;
            }
        }

        var tesseractResult = await _tesseract.RecognizeAsync(imageData, cancellationToken);
        if (IsResultAcceptable(tesseractResult))
        {
            return tesseractResult;
        }

        if (_paddle.IsAvailable)
        {
            return await _paddle.RecognizeAsync(imageData, cancellationToken);
        }

        return tesseractResult;
    }

    public async Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var data = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await RecognizeAsync(data, cancellationToken);
    }

    public async Task<OcrResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default)
    {
        var result = await _windows.RecognizeRegionAsync(imageData, region, cancellationToken);
        return IsResultAcceptable(result)
            ? result
            : await _paddle.RecognizeRegionAsync(imageData, region, cancellationToken);
    }

    public async Task<OcrResult> RecognizeWithTargetAsync(byte[] imageData, string targetText, CancellationToken cancellationToken = default)
    {
        var result = await RecognizeAsync(imageData, cancellationToken);
        if (result.FindFirstWord(targetText) is not null)
        {
            return result;
        }

        if (_paddle.IsAvailable)
        {
            return await _paddle.RecognizeAsync(imageData, cancellationToken);
        }

        return result;
    }

    private bool IsResultAcceptable(OcrResult result)
    {
        if (result.Confidence >= ConfidenceThreshold && !string.IsNullOrWhiteSpace(result.FullText))
        {
            return true;
        }

        if (!Options.EnablePreprocessing)
        {
            return false;
        }

        return (result.FullText?.Length ?? 0) >= MinTextLengthForFallback;
    }
}


