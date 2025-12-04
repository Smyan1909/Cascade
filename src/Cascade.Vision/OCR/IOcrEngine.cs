using Cascade.Vision.Capture;

namespace Cascade.Vision.OCR;

public interface IOcrEngine
{
    Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default);
    Task<OcrResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default);

    OcrOptions Options { get; set; }

    string EngineName { get; }
    IReadOnlyList<string> SupportedLanguages { get; }
    bool IsAvailable { get; }
}


