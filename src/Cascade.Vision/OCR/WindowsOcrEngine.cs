using System.Linq;
using Cascade.Vision.Capture;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Cascade.Vision.OCR;

public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly OcrEngine? _engine;

    public WindowsOcrEngine(OcrOptions? options = null)
    {
        Options = options ?? new OcrOptions();
        _engine = CreateEngine(Options.Language);
    }

    public string EngineName => "Windows.Media.Ocr";
    public IReadOnlyList<string> SupportedLanguages => _engine?.AvailableRecognizerLanguages.Select(lang => lang.LanguageTag).ToList() ?? Array.Empty<string>();
    public bool IsAvailable => _engine is not null;
    public OcrOptions Options { get; set; }

    public Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default)
        => RecognizeAsync(capture.ImageData, cancellationToken);

    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return OcrResultFromFailure("Engine unavailable");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(imageData);
            await writer.StoreAsync();
        }
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _engine!.RecognizeAsync(bitmap);
        stopwatch.Stop();

        return new OcrResult
        {
            FullText = result.Text ?? string.Empty,
            Confidence = CalculateConfidence(result),
            Lines = result.Lines.Select(ToLine).ToList(),
            Words = result.Lines.SelectMany(line => line.Words).Select(ToWord).ToList(),
            EngineUsed = EngineName,
            ProcessingTime = stopwatch.Elapsed
        };
    }

    public async Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var data = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await RecognizeAsync(data, cancellationToken);
    }

    public async Task<OcrResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(imageData);
        using var bitmap = new Bitmap(stream);
        var safeRegion = RegionSelector.ClampToBounds(region, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        using var cropped = bitmap.Clone(safeRegion, bitmap.PixelFormat);
        using var ms = new MemoryStream();
        cropped.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return await RecognizeAsync(ms.ToArray(), cancellationToken);
    }

    private static double CalculateConfidence(Windows.Media.Ocr.OcrResult result)
    {
        var words = result.Lines.SelectMany(line => line.Words).ToList();
        if (words.Count == 0)
        {
            return 0;
        }

        return words.Average(word => word.Confidence);
    }

    private static OcrLine ToLine(Windows.Media.Ocr.OcrLine line)
    {
        var bounds = line.Words.SelectMany(word => word.BoundingRect).ToList();
        return new OcrLine
        {
            Text = line.Text,
            BoundingBox = bounds.ToRectangle(),
            Confidence = line.Words.Count == 0 ? 0 : line.Words.Average(word => word.Confidence),
            Words = line.Words.Select(ToWord).ToList()
        };
    }

    private static OcrWord ToWord(Windows.Media.Ocr.OcrWord word)
    {
        return new OcrWord
        {
            Text = word.Text,
            BoundingBox = word.BoundingRect.ToRectangle(),
            Confidence = word.Confidence
        };
    }

    private static Rectangle ToRectangle(IReadOnlyList<Point?> points)
    {
        if (points.Count == 0)
        {
            return Rectangle.Empty;
        }

        var xs = points.Where(p => p.HasValue).Select(p => p.Value.X).ToArray();
        var ys = points.Where(p => p.HasValue).Select(p => p.Value.Y).ToArray();
        return Rectangle.FromLTRB((int)xs.Min(), (int)ys.Min(), (int)xs.Max(), (int)ys.Max());
    }

    private OcrResult OcrResultFromFailure(string reason) => new()
    {
        FullText = string.Empty,
        Confidence = 0,
        EngineUsed = $"{EngineName} ({reason})",
        Lines = Array.Empty<OcrLine>(),
        Words = Array.Empty<OcrWord>(),
        ProcessingTime = TimeSpan.Zero
    };

    private static OcrEngine? CreateEngine(string language)
    {
        try
        {
            var lang = new Language(language);
            return OcrEngine.TryCreateFromLanguage(lang) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch
        {
            return null;
        }
    }
}

internal static class OcrWordExtensions
{
    public static Rectangle ToRectangle(this IReadOnlyList<Point?> points)
    {
        if (points.Count == 0)
        {
            return Rectangle.Empty;
        }

        var xs = points.Where(p => p.HasValue).Select(p => p.Value.X).ToArray();
        var ys = points.Where(p => p.HasValue).Select(p => p.Value.Y).ToArray();
        return Rectangle.FromLTRB((int)xs.Min(), (int)ys.Min(), (int)xs.Max(), (int)ys.Max());
    }
}


