using System.Diagnostics;
using System.Drawing;
using Cascade.Vision.Capture;
using Cascade.Vision.Processing;
using Tesseract;

namespace Cascade.Vision.OCR;

/// <summary>
/// OCR engine using Tesseract (via Tesseract.NET).
/// Provides broad language support and high configurability.
/// </summary>
public class TesseractOcrEngine : IOcrEngine, IDisposable
{
    private TesseractEngine? _engine;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Gets or sets the path to the tessdata directory.
    /// </summary>
    public string TessDataPath { get; set; } = "./tessdata";

    /// <summary>
    /// Gets or sets the character whitelist (only recognize these characters).
    /// </summary>
    public string? Whitelist { get; set; }

    /// <summary>
    /// Gets or sets the character blacklist (don't recognize these characters).
    /// </summary>
    public string? Blacklist { get; set; }

    /// <inheritdoc />
    public string EngineName => "Tesseract";

    /// <inheritdoc />
    public OcrOptions Options { get; set; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedLanguages => GetAvailableLanguages();

    /// <inheritdoc />
    public bool IsAvailable => CheckAvailability();

    /// <inheritdoc />
    public int Priority => 2; // Second priority (after Windows OCR)

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => RecognizeCore(imageData), cancellationToken);
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
        return await Task.Run(() =>
        {
            // Crop the image first using ImageProcessor
            var processor = new ImageProcessor();
            var croppedData = processor.Crop(imageData, region);
            
            using var pix = LoadPix(croppedData);
            if (pix == null)
                return OcrResult.Empty(EngineName);

            return RecognizePixCore(pix, region.X, region.Y);
        }, cancellationToken);
    }

    private OcrResult RecognizeCore(byte[] imageData)
    {
        using var pix = LoadPix(imageData);
        if (pix == null)
            return OcrResult.Empty(EngineName);

        return RecognizePixCore(pix, 0, 0);
    }

    private OcrResult RecognizePixCore(Pix pix, int offsetX, int offsetY)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            EnsureEngine();
            if (_engine == null)
                return OcrResult.Empty(EngineName);

            lock (_lock)
            {
                using var page = _engine.Process(pix, ConvertPageSegMode(Options.PageSegMode));
                
                var fullText = page.GetText();
                var meanConfidence = page.GetMeanConfidence();

                var lines = ExtractLines(page, offsetX, offsetY);
                sw.Stop();

                return new OcrResult
                {
                    FullText = fullText.Trim(),
                    Confidence = meanConfidence,
                    Lines = lines,
                    ProcessingTime = sw.Elapsed,
                    EngineUsed = EngineName
                };
            }
        }
        catch (Exception)
        {
            sw.Stop();
            return OcrResult.Empty(EngineName);
        }
    }

    private List<OcrLine> ExtractLines(Page page, int offsetX, int offsetY)
    {
        var lines = new List<OcrLine>();

        try
        {
            using var iterator = page.GetIterator();
            iterator.Begin();

            do
            {
                if (iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var lineBounds))
                {
                    var lineText = iterator.GetText(PageIteratorLevel.TextLine);
                    var lineConfidence = iterator.GetConfidence(PageIteratorLevel.TextLine) / 100.0;

                    var words = ExtractWords(iterator, offsetX, offsetY);

                    lines.Add(new OcrLine
                    {
                        Text = lineText?.Trim() ?? string.Empty,
                        BoundingBox = new Rectangle(
                            lineBounds.X1 + offsetX,
                            lineBounds.Y1 + offsetY,
                            lineBounds.Width,
                            lineBounds.Height),
                        Confidence = lineConfidence,
                        Words = words
                    });
                }
            } while (iterator.Next(PageIteratorLevel.TextLine));
        }
        catch
        {
            // Fall back to simple text extraction if iteration fails
        }

        return lines;
    }

    private List<OcrWord> ExtractWords(ResultIterator lineIterator, int offsetX, int offsetY)
    {
        var words = new List<OcrWord>();

        try
        {
            // Create a copy of the iterator to traverse words within this line
            do
            {
                if (lineIterator.TryGetBoundingBox(PageIteratorLevel.Word, out var wordBounds))
                {
                    var wordText = lineIterator.GetText(PageIteratorLevel.Word);
                    var wordConfidence = lineIterator.GetConfidence(PageIteratorLevel.Word) / 100.0;

                    if (!string.IsNullOrWhiteSpace(wordText))
                    {
                        words.Add(new OcrWord
                        {
                            Text = wordText.Trim(),
                            BoundingBox = new Rectangle(
                                wordBounds.X1 + offsetX,
                                wordBounds.Y1 + offsetY,
                                wordBounds.Width,
                                wordBounds.Height),
                            Confidence = wordConfidence
                        });
                    }
                }
            } while (lineIterator.Next(PageIteratorLevel.Word) && 
                     !lineIterator.IsAtBeginningOf(PageIteratorLevel.TextLine));
        }
        catch
        {
            // Ignore word extraction errors
        }

        return words;
    }

    private void EnsureEngine()
    {
        if (_engine != null)
            return;

        lock (_lock)
        {
            if (_engine != null)
                return;

            try
            {
                var language = MapLanguage(Options.Language);
                _engine = new TesseractEngine(TessDataPath, language, EngineMode.Default);

                // Apply configuration
                if (!string.IsNullOrEmpty(Whitelist))
                    _engine.SetVariable("tessedit_char_whitelist", Whitelist);

                if (!string.IsNullOrEmpty(Blacklist))
                    _engine.SetVariable("tessedit_char_blacklist", Blacklist);
            }
            catch
            {
                _engine = null;
            }
        }
    }

    private static Pix? LoadPix(byte[] imageData)
    {
        try
        {
            return Pix.LoadFromMemory(imageData);
        }
        catch
        {
            return null;
        }
    }

    private static PageSegMode ConvertPageSegMode(PageSegmentationMode mode)
    {
        return mode switch
        {
            PageSegmentationMode.Auto => PageSegMode.Auto,
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
    }

    private static string MapLanguage(string language)
    {
        // Map common language codes to Tesseract language codes
        return language.ToLowerInvariant() switch
        {
            "en-us" or "en-gb" or "en" => "eng",
            "de-de" or "de" => "deu",
            "fr-fr" or "fr" => "fra",
            "es-es" or "es" => "spa",
            "it-it" or "it" => "ita",
            "pt-pt" or "pt-br" or "pt" => "por",
            "nl-nl" or "nl" => "nld",
            "pl-pl" or "pl" => "pol",
            "ru-ru" or "ru" => "rus",
            "ja-jp" or "ja" => "jpn",
            "ko-kr" or "ko" => "kor",
            "zh-cn" or "zh-tw" or "zh" => "chi_sim",
            "ar-sa" or "ar" => "ara",
            _ => "eng" // Default to English
        };
    }

    private bool CheckAvailability()
    {
        try
        {
            if (!Directory.Exists(TessDataPath))
                return false;

            var language = MapLanguage(Options.Language);
            var trainedDataPath = Path.Combine(TessDataPath, $"{language}.traineddata");
            return File.Exists(trainedDataPath);
        }
        catch
        {
            return false;
        }
    }

    private IReadOnlyList<string> GetAvailableLanguages()
    {
        try
        {
            if (!Directory.Exists(TessDataPath))
                return Array.Empty<string>();

            return Directory.GetFiles(TessDataPath, "*.traineddata")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Disposes of the Tesseract engine.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _engine?.Dispose();
            _engine = null;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}

