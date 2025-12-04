using Cascade.Tests.Vision;
using Cascade.Vision.OCR;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Vision.OCR;

public class CompositeOcrEngineTests
{
    [Fact]
    public async Task RecognizeAsync_UsesFirstEngineMeetingConfidence()
    {
        var lowConfidence = new FakeEngine(new OcrResult { Confidence = 0.2, FullText = string.Empty, EngineUsed = "Windows" }, isAvailable: true);
        var strongResult = new FakeEngine(new OcrResult { Confidence = 0.95, FullText = "Submit", EngineUsed = "Tesseract" }, isAvailable: true);
        var paddle = new FakeEngine(new OcrResult { Confidence = 0.99, FullText = "Fallback", EngineUsed = "Paddle" }, isAvailable: true);

        var engine = new CompositeOcrEngine(lowConfidence, strongResult, paddle);
        var image = TestImageFactory.CreateSolidColor(System.Drawing.Color.Black);

        var result = await engine.RecognizeAsync(image);

        result.EngineUsed.Should().Be("Tesseract");
        result.FullText.Should().Be("Submit");
    }

    [Fact]
    public async Task RecognizeWithTargetAsync_FallsBackToPaddleWhenNotFound()
    {
        var windows = new FakeEngine(new OcrResult { Confidence = 0.9, FullText = "Alpha" }, isAvailable: true);
        var tesseract = new FakeEngine(new OcrResult { Confidence = 0.9, FullText = "Beta" }, isAvailable: true);
        var paddle = new FakeEngine(new OcrResult
        {
            Confidence = 0.95,
            FullText = "Gamma Target",
            Words = new[] { new OcrWord { Text = "Target", BoundingBox = new System.Drawing.Rectangle(1, 1, 5, 5), Confidence = 0.95 } }
        }, isAvailable: true);

        var engine = new CompositeOcrEngine(windows, tesseract, paddle);
        var image = TestImageFactory.CreateSolidColor(System.Drawing.Color.White);

        var result = await engine.RecognizeWithTargetAsync(image, "Target");

        result.EngineUsed.Should().Be("PaddleOCR");
        result.FindFirstWord("Target").Should().NotBeNull();
    }

    private sealed class FakeEngine : IOcrEngine
    {
        private readonly OcrResult _result;

        public FakeEngine(OcrResult result, bool isAvailable)
        {
            _result = result;
            IsAvailable = isAvailable;
            Options = new OcrOptions();
        }

        public string EngineName => _result.EngineUsed ?? "Fake";
        public IReadOnlyList<string> SupportedLanguages { get; } = new[] { "en" };
        public bool IsAvailable { get; }
        public OcrOptions Options { get; set; }

        public Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default) => Task.FromResult(_result);
        public Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default) => Task.FromResult(_result);
        public Task<OcrResult> RecognizeAsync(Cascade.Vision.Capture.CaptureResult capture, CancellationToken cancellationToken = default) => Task.FromResult(_result);
        public Task<OcrResult> RecognizeRegionAsync(byte[] imageData, System.Drawing.Rectangle region, CancellationToken cancellationToken = default) => Task.FromResult(_result);
    }
}


