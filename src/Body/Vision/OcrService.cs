using Cascade.Body.Configuration;
using Cascade.Proto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Drawing;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Cascade.Body.Vision;

public class OcrService
{
    private readonly OcrOptions _options;
    private readonly ILogger<OcrService> _logger;
    private OcrEngine? _engine;

    public OcrService(IOptions<OcrOptions> options, ILogger<OcrService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    public async Task<OcrResultData> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        if (!IsEnabled || imageBytes.Length == 0)
        {
            return OcrResultData.Empty;
        }

        try
        {
            using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await ras.WriteAsync(imageBytes.AsBuffer()).AsTask(cancellationToken).ConfigureAwait(false);
            ras.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(ras).AsTask(cancellationToken).ConfigureAwait(false);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken).ConfigureAwait(false);

            var result = await GetEngine().RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);
            return MapResult(result, (int)decoder.PixelWidth, (int)decoder.PixelHeight);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR extraction failed");
            return OcrResultData.Empty;
        }
    }

    public async Task<OcrResultData> ExtractFromBitmapAsync(Bitmap bitmap, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return OcrResultData.Empty;
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, DrawingImageFormat.Png);
        return await ExtractAsync(ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private OcrEngine GetEngine()
    {
        if (_engine != null)
        {
            return _engine;
        }

        if (!string.IsNullOrWhiteSpace(_options.LanguageTag))
        {
            var lang = new Language(_options.LanguageTag);
            _engine = OcrEngine.TryCreateFromLanguage(lang) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        }
        else
        {
            _engine = OcrEngine.TryCreateFromUserProfileLanguages();
        }

        _engine ??= OcrEngine.TryCreateFromLanguage(new Language("en-US"));
        return _engine;
    }

    private static OcrResultData MapResult(OcrResult result, int width, int height)
    {
        var regions = new List<OcrRegion>();
        foreach (var line in result.Lines)
        {
            if (line.Words.Count == 0)
            {
                continue;
            }

            var bounds = line.Words.Select(w => w.BoundingRect).ToList();
            var minX = bounds.Min(b => b.X);
            var minY = bounds.Min(b => b.Y);
            var maxX = bounds.Max(b => b.X + b.Width);
            var maxY = bounds.Max(b => b.Y + b.Height);
            var rect = new NormalizedRectangle
            {
                X = Clamp(minX / width),
                Y = Clamp(minY / height),
                Width = Clamp((maxX - minX) / width),
                Height = Clamp((maxY - minY) / height)
            };
            var text = string.Join(" ", line.Words.Select(w => w.Text));
            regions.Add(new OcrRegion(rect, text));
        }

        var concatenated = string.Join(" ", regions.Select(r => r.Text));
        return new OcrResultData(concatenated, regions);
    }

    private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
}

public record OcrRegion(NormalizedRectangle Bounds, string Text);

public record OcrResultData(string Text, IReadOnlyList<OcrRegion> Regions)
{
    public static OcrResultData Empty { get; } = new(string.Empty, Array.Empty<OcrRegion>());
}

