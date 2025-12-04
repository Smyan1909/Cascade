using System.Linq;
using Cascade.Vision.Capture;
using Cascade.Vision.Protos;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ProtoModel = Cascade.Vision.Protos.PaddleOcrModel;

namespace Cascade.Vision.OCR;

public sealed class PaddleOcrEngine : IOcrEngine, IDisposable
{
    private readonly ILogger<PaddleOcrEngine>? _logger;
    private readonly PaddleOcrOptions _options;
    private readonly GrpcChannel _channel;
    private readonly PaddleOcrService.PaddleOcrServiceClient _client;

    public PaddleOcrEngine(PaddleOcrOptions? options = null, OcrOptions? ocrOptions = null, ILogger<PaddleOcrEngine>? logger = null)
    {
        _options = options ?? new PaddleOcrOptions();
        Options = ocrOptions ?? new OcrOptions();
        _logger = logger;
        _channel = GrpcChannel.ForAddress(_options.ServiceEndpoint);
        _client = new PaddleOcrService.PaddleOcrServiceClient(_channel);
    }

    public string EngineName => "PaddleOCR";
    public IReadOnlyList<string> SupportedLanguages { get; } = new[] { "en", "ch", "japan", "korean", "french", "german", "arabic", "cyrillic", "latin", "devanagari" };
    public bool IsAvailable => CheckHealth();
    public OcrOptions Options { get; set; }
    public PaddleOcrModel Model { get; set; } = PaddleOcrModel.PPOCRv4;

    public Task<OcrResult> RecognizeAsync(CaptureResult capture, CancellationToken cancellationToken = default)
        => RecognizeAsync(capture.ImageData, cancellationToken);

    public async Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var data = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await RecognizeAsync(data, cancellationToken);
    }

    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = new PaddleOcrRequest
        {
            ImageData = Google.Protobuf.ByteString.CopyFrom(imageData),
            Language = Options.Language ?? _options.DefaultLanguage,
            Model = MapModel(Model),
            UseAngleClassifier = _options.UseAngleClassifier,
            DetectOnly = false
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        PaddleOcrResponse response;

        try
        {
            var call = _client.RecognizeAsync(request, cancellationToken: cancellationToken, deadline: DateTime.UtcNow + _options.RequestTimeout);
            response = await call.ResponseAsync.ConfigureAwait(false);
        }
        catch (Exception ex) when (_options.EnableRetry)
        {
            _logger?.LogWarning(ex, "PaddleOCR request failed. Retrying up to {MaxRetries} times.", _options.MaxRetries);
            response = await RetryAsync(request, cancellationToken);
        }

        stopwatch.Stop();

        if (!response.Success)
        {
            return new OcrResult
            {
                FullText = string.Empty,
                Confidence = 0,
                EngineUsed = $"{EngineName} (error: {response.ErrorMessage})",
                Lines = Array.Empty<OcrLine>(),
                Words = Array.Empty<OcrWord>(),
                ProcessingTime = stopwatch.Elapsed
            };
        }

        var lines = response.Lines.Select(line => new OcrLine
        {
            Text = line.Text,
            BoundingBox = BuildRectangle(line.Polygon),
            Confidence = line.Confidence,
            Words = line.Words.Select(w => new OcrWord
            {
                Text = w.Text,
                BoundingBox = BuildRectangle(w.Polygon),
                Confidence = w.Confidence
            }).ToList()
        }).ToList();

        var words = lines.SelectMany(l => l.Words).ToList();

        return new OcrResult
        {
            FullText = response.FullText,
            Confidence = response.Confidence,
            Lines = lines,
            Words = words,
            ProcessingTime = stopwatch.Elapsed,
            EngineUsed = response.ModelUsed ?? EngineName
        };
    }

    public Task<OcrResult> RecognizeRegionAsync(byte[] imageData, Rectangle region, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(imageData);
        using var bitmap = new Bitmap(stream);
        var bounded = RegionSelector.ClampToBounds(region, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        using var cropped = bitmap.Clone(bounded, bitmap.PixelFormat);
        using var ms = new MemoryStream();
        cropped.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return RecognizeAsync(ms.ToArray(), cancellationToken);
    }

    private async Task<PaddleOcrResponse> RetryAsync(PaddleOcrRequest request, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < _options.MaxRetries; attempt++)
        {
            try
            {
                var call = _client.RecognizeAsync(request, cancellationToken: cancellationToken, deadline: DateTime.UtcNow + _options.RequestTimeout);
                return await call.ResponseAsync.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger?.LogWarning(ex, "PaddleOCR retry {Attempt} failed.", attempt + 1);
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("PaddleOCR recognition failed after retries", lastError);
    }

    private static Rectangle BuildRectangle(IEnumerable<PaddleOcrPoint> points)
    {
        var list = points.ToList();
        if (list.Count == 0)
        {
            return Rectangle.Empty;
        }

        var xs = list.Select(p => p.X);
        var ys = list.Select(p => p.Y);
        return Rectangle.FromLTRB(xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    private ProtoModel MapModel(PaddleOcrModel model) => model switch
    {
        PaddleOcrModel.SVTR => ProtoModel.PaddleOcrModelSvtr,
        PaddleOcrModel.ViTSTR => ProtoModel.PaddleOcrModelVitstr,
        _ => ProtoModel.PaddleOcrModelPpOcrv4
    };

    private bool CheckHealth()
    {
        try
        {
            var status = _client.GetStatus(new Empty(), deadline: DateTime.UtcNow + _options.ConnectionTimeout);
            if (!string.IsNullOrWhiteSpace(status.ModelLoaded))
            {
                Model = status.ModelLoaded.ToUpperInvariant() switch
                {
                    "SVTR" => PaddleOcrModel.SVTR,
                    "VITSTR" => PaddleOcrModel.ViTSTR,
                    _ => PaddleOcrModel.PPOCRv4
                };
            }
            return status.IsReady;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Unable to reach PaddleOCR service at {Endpoint}", _options.ServiceEndpoint);
            return false;
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}


