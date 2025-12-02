using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;
using Cascade.Vision.Capture;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Cascade.Vision.OCR;

/// <summary>
/// OCR engine using Windows.Media.Ocr (Windows 10+ built-in OCR).
/// This is the fastest option with no external dependencies.
/// </summary>
public class WindowsOcrEngine : IOcrEngine
{
    private readonly Lazy<OcrEngine?> _engine;
    private readonly Lazy<IReadOnlyList<string>> _supportedLanguages;

    /// <summary>
    /// Creates a new WindowsOcrEngine.
    /// </summary>
    public WindowsOcrEngine()
    {
        _engine = new Lazy<OcrEngine?>(() => CreateEngine());
        _supportedLanguages = new Lazy<IReadOnlyList<string>>(() => GetAvailableLanguages());
    }

    /// <inheritdoc />
    public string EngineName => "Windows.Media.Ocr";

    /// <inheritdoc />
    public OcrOptions Options { get; set; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedLanguages => _supportedLanguages.Value;

    /// <inheritdoc />
    public bool IsAvailable => _engine.Value != null;

    /// <inheritdoc />
    public int Priority => 1; // Highest priority (fastest)

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return OcrResult.Empty(EngineName);

        var sw = Stopwatch.StartNew();

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(imageData.AsBuffer());
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            var result = await _engine.Value!.RecognizeAsync(softwareBitmap);
            sw.Stop();

            return ConvertResult(result, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new OcrResult
            {
                FullText = string.Empty,
                Confidence = 0,
                EngineUsed = EngineName,
                ProcessingTime = sw.Elapsed,
                Lines = Array.Empty<OcrLine>()
            };
        }
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
        if (!IsAvailable)
            return OcrResult.Empty(EngineName);

        var sw = Stopwatch.StartNew();

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(imageData.AsBuffer());
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            
            // Crop to region
            var transform = new BitmapTransform
            {
                Bounds = new BitmapBounds
                {
                    X = (uint)Math.Max(0, region.X),
                    Y = (uint)Math.Max(0, region.Y),
                    Width = (uint)Math.Min(region.Width, decoder.PixelWidth - region.X),
                    Height = (uint)Math.Min(region.Height, decoder.PixelHeight - region.Y)
                }
            };

            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            var result = await _engine.Value!.RecognizeAsync(softwareBitmap);
            sw.Stop();

            var ocrResult = ConvertResult(result, sw.Elapsed);
            
            // Adjust bounding boxes to original image coordinates
            AdjustCoordinates(ocrResult, region.X, region.Y);
            
            return ocrResult;
        }
        catch
        {
            sw.Stop();
            return OcrResult.Empty(EngineName);
        }
    }

    private OcrEngine? CreateEngine()
    {
        try
        {
            // Try to get engine for specified language
            var language = new Windows.Globalization.Language(Options.Language);
            if (OcrEngine.IsLanguageSupported(language))
            {
                return OcrEngine.TryCreateFromLanguage(language);
            }

            // Fallback to user profile languages
            return OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> GetAvailableLanguages()
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages
                .Select(l => l.LanguageTag)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private OcrResult ConvertResult(Windows.Media.Ocr.OcrResult result, TimeSpan processingTime)
    {
        var lines = new List<OcrLine>();
        var fullText = new System.Text.StringBuilder();
        double totalConfidence = 0;
        int wordCount = 0;

        foreach (var line in result.Lines)
        {
            var words = new List<OcrWord>();
            
            foreach (var word in line.Words)
            {
                var ocrWord = new OcrWord
                {
                    Text = word.Text,
                    BoundingBox = new Rectangle(
                        (int)word.BoundingRect.X,
                        (int)word.BoundingRect.Y,
                        (int)word.BoundingRect.Width,
                        (int)word.BoundingRect.Height),
                    Confidence = 0.95 // Windows OCR doesn't provide per-word confidence
                };
                words.Add(ocrWord);
                wordCount++;
            }

            // Calculate line bounding box
            var lineBounds = CalculateBoundingBox(words.Select(w => w.BoundingBox));

            var ocrLine = new OcrLine
            {
                Text = line.Text,
                BoundingBox = lineBounds,
                Confidence = 0.95, // Windows OCR doesn't provide per-line confidence
                Words = words
            };
            lines.Add(ocrLine);

            if (fullText.Length > 0)
                fullText.AppendLine();
            fullText.Append(line.Text);

            totalConfidence += 0.95 * words.Count;
        }

        return new OcrResult
        {
            FullText = fullText.ToString(),
            Confidence = wordCount > 0 ? totalConfidence / wordCount : 0,
            Lines = lines,
            ProcessingTime = processingTime,
            EngineUsed = EngineName
        };
    }

    private static Rectangle CalculateBoundingBox(IEnumerable<Rectangle> rectangles)
    {
        var rects = rectangles.ToList();
        if (rects.Count == 0)
            return Rectangle.Empty;

        int minX = rects.Min(r => r.Left);
        int minY = rects.Min(r => r.Top);
        int maxX = rects.Max(r => r.Right);
        int maxY = rects.Max(r => r.Bottom);

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AdjustCoordinates(OcrResult result, int offsetX, int offsetY)
    {
        var adjustedLines = new List<OcrLine>();
        
        foreach (var line in result.Lines)
        {
            var adjustedWords = line.Words.Select(w => new OcrWord
            {
                Text = w.Text,
                BoundingBox = new Rectangle(
                    w.BoundingBox.X + offsetX,
                    w.BoundingBox.Y + offsetY,
                    w.BoundingBox.Width,
                    w.BoundingBox.Height),
                Confidence = w.Confidence
            }).ToList();

            adjustedLines.Add(new OcrLine
            {
                Text = line.Text,
                BoundingBox = new Rectangle(
                    line.BoundingBox.X + offsetX,
                    line.BoundingBox.Y + offsetY,
                    line.BoundingBox.Width,
                    line.BoundingBox.Height),
                Confidence = line.Confidence,
                Words = adjustedWords
            });
        }

        // Since Lines is IReadOnlyList, we need to create a new OcrResult
        // In this implementation, we modify in place for simplicity
        var linesField = typeof(OcrResult).GetProperty(nameof(OcrResult.Lines));
        linesField?.SetValue(result, adjustedLines);
    }
}

