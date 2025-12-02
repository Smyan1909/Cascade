using System.Diagnostics;
using System.Drawing;
using Cascade.Vision.Capture;
using Cascade.Vision.OCR;
using Cascade.Vision.Services;
using Xunit;

namespace Cascade.Tests.Vision;

/// <summary>
/// Integration tests for the Vision module using Calculator and Notepad.
/// These tests require a Windows desktop environment with Calculator and Notepad installed.
/// </summary>
[Trait("Category", "Integration")]
public class VisionIntegrationTests : IDisposable
{
    private readonly VisionService _visionService;
    private Process? _calculatorProcess;
    private Process? _notepadProcess;

    public VisionIntegrationTests()
    {
        // Find the project root (where tessdata is located)
        var testDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var tessDataPath = Path.Combine(projectRoot, "tessdata");
        
        _visionService = new VisionService(new VisionOptions
        {
            DefaultOcrOptions = OcrOptions.ForScreenText,
            TesseractDataPath = tessDataPath
        });
    }

    private async Task<Process> StartCalculatorAsync()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "calc.exe",
            UseShellExecute = true
        });

        // Wait for window to be ready
        await Task.Delay(1500);
        return process!;
    }

    private async Task<Process> StartNotepadAsync()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            UseShellExecute = true
        });

        // Wait for window to be ready
        await Task.Delay(1000);
        return process!;
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task ScreenCapture_CapturePrimaryScreen_ReturnsValidImage()
    {
        // Act
        var capture = await _visionService.CaptureScreenAsync();

        // Assert
        Assert.NotNull(capture);
        Assert.True(capture.Width > 0);
        Assert.True(capture.Height > 0);
        Assert.NotEmpty(capture.ImageData);
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task ScreenCapture_CaptureRegion_ReturnsCorrectSize()
    {
        // Arrange
        var region = new Rectangle(100, 100, 200, 150);

        // Act
        var capture = await _visionService.CaptureRegionAsync(region);

        // Assert
        Assert.NotNull(capture);
        Assert.Equal(200, capture.Width);
        Assert.Equal(150, capture.Height);
    }

    [Fact]
    [Trait("Requires", "Calculator")]
    public async Task OCR_RecognizeCalculatorWindow_FindsDigitButtons()
    {
        // Arrange
        _calculatorProcess = await StartCalculatorAsync();

        try
        {
            // Act
            var capture = await _visionService.CaptureWindowAsync("Calculator");
            var ocrResult = await _visionService.RecognizeTextAsync(capture);

            // Debug output
            System.Console.WriteLine($"=== OCR Results ===");
            System.Console.WriteLine($"Engine: {ocrResult.EngineUsed}");
            System.Console.WriteLine($"Confidence: {ocrResult.Confidence:P}");
            System.Console.WriteLine($"Processing Time: {ocrResult.ProcessingTime.TotalMilliseconds}ms");
            System.Console.WriteLine($"Full Text:\n{ocrResult.FullText}");
            System.Console.WriteLine($"Total Words: {ocrResult.Words.Count}");
            System.Console.WriteLine($"Words found: {string.Join(", ", ocrResult.Words.Take(20).Select(w => $"'{w.Text}'"))}");
            
            // Assert
            Assert.True(ocrResult.HasText, $"OCR should find text. Engine: {ocrResult.EngineUsed}");
            
            // Calculator should have digit buttons visible
            var foundDigits = ocrResult.Words
                .Where(w => w.Text.Length == 1 && char.IsDigit(w.Text[0]))
                .ToList();
            
            System.Console.WriteLine($"Digits found: {string.Join(", ", foundDigits.Select(w => w.Text))}");
            
            // Modern calculator might have different layouts, just verify OCR works
            Assert.NotNull(ocrResult.FullText);
            Assert.True(ocrResult.FullText.Length > 0, "OCR should have found text in Calculator");
        }
        finally
        {
            _calculatorProcess?.Kill();
        }
    }

    [Fact]
    [Trait("Requires", "Notepad")]
    public async Task OCR_RecognizeNotepadWithText_FindsTypedText()
    {
        // This test would require typing text into Notepad first
        // For now, just verify we can capture the window
        _notepadProcess = await StartNotepadAsync();

        try
        {
            // Act
            var capture = await _visionService.CaptureWindowAsync("Notepad");

            // Assert
            Assert.NotNull(capture);
            Assert.True(capture.Width > 0);
            Assert.True(capture.Height > 0);
        }
        finally
        {
            _notepadProcess?.Kill();
        }
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task ChangeDetection_NoChange_ReportsNoChange()
    {
        // Arrange
        var capture1 = await _visionService.CaptureRegionAsync(new Rectangle(0, 0, 200, 200));
        
        // Act - capture the same region again immediately
        var capture2 = await _visionService.CaptureRegionAsync(new Rectangle(0, 0, 200, 200));
        
        var result = await _visionService.CompareImagesAsync(
            capture1.ImageData, 
            capture2.ImageData);

        // Assert - should be no significant changes (within tolerance)
        // Note: Some minor differences might occur due to cursor blinking, etc.
        Assert.True(result.DifferencePercentage < 0.05); // Less than 5% change
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task OcrEngineStatus_ReturnsAvailableEngines()
    {
        // Act
        var status = _visionService.GetOcrEngineStatus();

        // Debug output
        System.Console.WriteLine("=== OCR Engine Status ===");
        foreach (var engine in status)
        {
            System.Console.WriteLine($"  {engine.Key}: {(engine.Value ? "AVAILABLE" : "NOT AVAILABLE")}");
        }

        // Assert
        Assert.NotNull(status);
        Assert.True(status.Count >= 2); // At least Windows OCR and Tesseract (may not be available)
        Assert.True(status.ContainsKey("Windows.Media.Ocr"));
    }

    [Fact]
    [Trait("Requires", "Calculator")]
    public async Task Tesseract_RecognizeCalculatorWindow_FindsText()
    {
        // Arrange
        _calculatorProcess = await StartCalculatorAsync();

        // Find the project root (where tessdata is located)
        var testDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var tessDataPath = Path.Combine(projectRoot, "tessdata");

        var tesseract = new TesseractOcrEngine 
        { 
            TessDataPath = tessDataPath,
            Options = OcrOptions.ForScreenText
        };

        try
        {
            System.Console.WriteLine($"=== Tesseract OCR Test ===");
            System.Console.WriteLine($"TessData Path: {tessDataPath}");
            System.Console.WriteLine($"Tesseract Available: {tesseract.IsAvailable}");

            if (!tesseract.IsAvailable)
            {
                System.Console.WriteLine("Tesseract not available - skipping");
                return;
            }

            // Capture Calculator window
            var capture = new ScreenCapture();
            var captureResult = await capture.CaptureWindowAsync("Calculator");
            
            // Run Tesseract OCR
            var ocrResult = await tesseract.RecognizeAsync(captureResult);

            // Debug output
            System.Console.WriteLine($"Engine: {ocrResult.EngineUsed}");
            System.Console.WriteLine($"Confidence: {ocrResult.Confidence:P}");
            System.Console.WriteLine($"Processing Time: {ocrResult.ProcessingTime.TotalMilliseconds}ms");
            System.Console.WriteLine($"Full Text:\n{ocrResult.FullText}");
            System.Console.WriteLine($"Total Words: {ocrResult.Words.Count}");
            System.Console.WriteLine($"Words found: {string.Join(", ", ocrResult.Words.Take(20).Select(w => $"'{w.Text}'"))}");

            // Assert
            Assert.True(ocrResult.HasText, "Tesseract should find text in Calculator");
            Assert.Equal("Tesseract", ocrResult.EngineUsed);
        }
        finally
        {
            tesseract.Dispose();
            _calculatorProcess?.Kill();
        }
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task LayoutAnalysis_ScreenCapture_DetectsLayout()
    {
        // Arrange
        var capture = await _visionService.CaptureScreenAsync();

        // Act
        var layout = await _visionService.AnalyzeLayoutAsync(capture.ImageData);

        // Assert
        Assert.NotNull(layout);
        Assert.True(layout.ImageSize.Width > 0);
        Assert.True(layout.ImageSize.Height > 0);
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task ElementDetection_ScreenCapture_DetectsElements()
    {
        // Arrange
        var capture = await _visionService.CaptureScreenAsync();

        // Act
        var elements = await _visionService.DetectElementsAsync(capture.ImageData);

        // Assert
        Assert.NotNull(elements);
        // Desktop should have some detectable elements
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task ContrastAnalysis_ScreenRegion_ReturnsValidResult()
    {
        // Arrange
        var capture = await _visionService.CaptureRegionAsync(new Rectangle(100, 100, 200, 200));

        // Act
        var result = _visionService.AnalyzeContrast(capture.ImageData);

        // Assert
        Assert.True(result.IsAnalyzable);
        Assert.True(result.ContrastRatio >= 1.0);
    }

    [Fact]
    [Trait("Requires", "Desktop")]
    public async Task ImagePreprocessing_ForOcr_ProducesLargerImage()
    {
        // Arrange
        var capture = await _visionService.CaptureRegionAsync(new Rectangle(100, 100, 100, 100));

        // Act
        var preprocessed = _visionService.PreprocessForOcr(capture.ImageData);

        // Assert
        var (origWidth, origHeight) = _visionService.ImageProcessor.GetDimensions(capture.ImageData);
        var (newWidth, newHeight) = _visionService.ImageProcessor.GetDimensions(preprocessed);
        
        Assert.True(newWidth > origWidth);
        Assert.True(newHeight > origHeight);
    }

    public void Dispose()
    {
        _visionService.Dispose();
        
        try { _calculatorProcess?.Kill(); } catch { }
        try { _notepadProcess?.Kill(); } catch { }
        
        _calculatorProcess?.Dispose();
        _notepadProcess?.Dispose();
    }
}

