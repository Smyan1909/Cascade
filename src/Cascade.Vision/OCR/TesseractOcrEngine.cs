using System.Diagnostics;
using System.Linq;
using Cascade.Vision.Capture;
using Tesseract;

namespace Cascade.Vision.OCR;

public sealed class TesseractOcrEngine : IOcrEngine, IDisposable
{
    private TesseractEngine? _engine;

    public TesseractOcrEngine(OcrOptions? options = null)
    {
        Options = options ?? new OcrOptions();
    }

    public string EngineName => "Tesseract";
    public IReadOnlyList<string> SupportedLanguages => new[] { Options.Language };
    public bool IsAvailable => TryEnsureEngine();
    public OcrOptions Options { get; set; }

    public string TessDataPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "tessdata");
    public string? Whitelist { get; set; }
    public string? Blacklist { get; set; }

    public Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default)
        => RecognizeAsync(capture.ImageData, cancellationToken);

    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryEnsureEngine())
        {
            return EmptyResult("Engine unavailable");
        }

        using var pix = Pix.LoadFromMemory(imageData);
        return await Task.Run(() => ProcessPix(pix), cancellationToken);
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

    private OcrResult ProcessPix(Pix pix)
    {
        var engine = _engine ?? throw new InvalidOperationException("Tesseract engine not initialized.");
        var stopwatch = Stopwatch.StartNew();
        using var page = engine.Process(pix, MapPageSegMode(Options.PageSegMode));
        var iterator = page.GetIterator();
        var lines = new List<OcrLine>();
        var words = new List<OcrWord>();

        if (iterator is not null)
        {
            iterator.Begin();
            do
            {
                if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine))
                {
                    var lineText = iterator.GetText(PageIteratorLevel.TextLine) ?? string.Empty;
                    var lineBounds = iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var lb) ? lb.ToRectangle() : Rectangle.Empty;
                    var lineWords = new List<OcrWord>();

                    do
                    {
                        if (!iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var wb))
                        {
                            continue;
                        }

                        var wordText = iterator.GetText(PageIteratorLevel.Word) ?? string.Empty;
                        var confidence = iterator.Confidence(PageIteratorLevel.Word) / 100.0;
                        var word = new OcrWord
                        {
                            Text = wordText.Trim(),
                            BoundingBox = wb.ToRectangle(),
                            Confidence = confidence
                        };
                        lineWords.Add(word);
                        words.Add(word);
                    }
                    while (iterator.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                    lines.Add(new OcrLine
                    {
                        Text = lineText.Trim(),
                        BoundingBox = lineBounds,
                        Confidence = lineWords.Count == 0 ? 0 : lineWords.Average(w => w.Confidence),
                        Words = lineWords
                    });
                }
            }
            while (iterator.Next(PageIteratorLevel.Block, PageIteratorLevel.TextLine));
        }

        stopwatch.Stop();

        return new OcrResult
        {
            FullText = page.GetText() ?? string.Empty,
            Confidence = page.GetMeanConfidence(),
            Lines = lines,
            Words = words,
            EngineUsed = EngineName,
            ProcessingTime = stopwatch.Elapsed
        };
    }

    private bool TryEnsureEngine()
    {
        if (_engine is not null)
        {
            return true;
        }

        if (!Directory.Exists(TessDataPath))
        {
            return false;
        }

        try
        {
            _engine = new TesseractEngine(TessDataPath, Options.Language, EngineMode.Default);
            if (!string.IsNullOrWhiteSpace(Whitelist))
            {
                _engine.SetVariable("tessedit_char_whitelist", Whitelist);
            }

            if (!string.IsNullOrWhiteSpace(Blacklist))
            {
                _engine.SetVariable("tessedit_char_blacklist", Blacklist);
            }

            return true;
        }
        catch
        {
            _engine = null;
            return false;
        }
    }

    private static PageSegMode MapPageSegMode(PageSegmentationMode segMode) => segMode switch
    {
        PageSegmentationMode.SingleBlock => PageSegMode.SingleBlock,
        PageSegmentationMode.SingleColumn => PageSegMode.SingleColumn,
        PageSegmentationMode.SingleLine => PageSegMode.SingleLine,
        PageSegmentationMode.SingleWord => PageSegMode.SingleWord,
        PageSegmentationMode.CircleWord => PageSegMode.CircleWord,
        PageSegmentationMode.SingleChar => PageSegMode.SingleChar,
        PageSegmentationMode.SparseText => PageSegMode.SparseText,
        PageSegmentationMode.SparseTextOsd => PageSegMode.SparseTextOsd,
        _ => PageSegMode.Auto
    };

    private OcrResult EmptyResult(string reason) => new()
    {
        FullText = string.Empty,
        Confidence = 0,
        EngineUsed = $"{EngineName} ({reason})",
        Lines = Array.Empty<OcrLine>(),
        Words = Array.Empty<OcrWord>(),
        ProcessingTime = TimeSpan.Zero
    };

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}

internal static class RectExtensions
{
    public static Rectangle ToRectangle(this Rect rect)
        => Rectangle.FromLTRB((int)rect.X1, (int)rect.Y1, (int)rect.X2, (int)rect.Y2);
}


