using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using Cascade.Vision.OCR;

namespace Cascade.Vision.Services;

public class VisionOptions
{
    public CaptureOptions DefaultCaptureOptions { get; set; } = new();
    public bool ForceHiddenDesktopCapture { get; set; } = true;
    public TimeSpan SessionFrameTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public OcrOptions DefaultOcrOptions { get; set; } = new();
    public string? TesseractDataPath { get; set; }
    public PaddleOcrOptions PaddleOcr { get; set; } = new();
    public ComparisonOptions DefaultComparisonOptions { get; set; } = new();
    public bool EnableCaching { get; set; } = true;
    public int MaxCachedScreenshots { get; set; } = 10;
    public bool SaveDebugImages { get; set; }
    public string? DebugImagePath { get; set; }
}


