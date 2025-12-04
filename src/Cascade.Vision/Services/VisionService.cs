using System.Collections.Concurrent;
using System.Linq;
using Cascade.Vision.Analysis;
using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using Cascade.Vision.OCR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cascade.Vision.Services;

public sealed class VisionService
{
    private readonly ISessionFrameProvider _frameProvider;
    private readonly IOcrEngine _ocrEngine;
    private readonly IElementAnalyzer _elementAnalyzer;
    private readonly IChangeDetector _changeDetector;
    private readonly VisionOptions _options;
    private readonly ILogger<VisionService>? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ConcurrentDictionary<Guid, CaptureResult> _cache = new();

    public VisionService(
        ISessionFrameProvider frameProvider,
        IOcrEngine ocrEngine,
        IElementAnalyzer elementAnalyzer,
        IChangeDetector changeDetector,
        IOptions<VisionOptions>? options = null,
        ILogger<VisionService>? logger = null,
        ILoggerFactory? loggerFactory = null)
    {
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _elementAnalyzer = elementAnalyzer ?? throw new ArgumentNullException(nameof(elementAnalyzer));
        _changeDetector = changeDetector ?? throw new ArgumentNullException(nameof(changeDetector));
        _options = options?.Value ?? new VisionOptions();
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<CaptureResult> CaptureScreenAsync(SessionHandle session, int screenIndex = 0, CancellationToken cancellationToken = default)
    {
        var capture = await CreateCapture(session).CaptureScreenAsync(screenIndex, cancellationToken);
        Cache(session, capture);
        return capture;
    }

    public async Task<OcrResult> RecognizeAsync(SessionHandle session, CaptureResult? capture = null, CancellationToken cancellationToken = default)
    {
        capture ??= await CaptureScreenAsync(session, cancellationToken: cancellationToken);
        return await _ocrEngine.RecognizeAsync(capture, cancellationToken);
    }

    public async Task<IReadOnlyList<VisualElement>> AnalyzeElementsAsync(SessionHandle session, CaptureResult? capture = null, CancellationToken cancellationToken = default)
    {
        capture ??= await CaptureScreenAsync(session, cancellationToken: cancellationToken);
        return await _elementAnalyzer.DetectElementsAsync(capture, cancellationToken);
    }

    public async Task<LayoutAnalysis> AnalyzeLayoutAsync(SessionHandle session, CaptureResult? capture = null, CancellationToken cancellationToken = default)
    {
        capture ??= await CaptureScreenAsync(session, cancellationToken: cancellationToken);
        return await _elementAnalyzer.AnalyzeLayoutAsync(capture.ImageData, cancellationToken);
    }

    public async Task<ChangeResult> WaitForChangeAsync(SessionHandle session, Rectangle region, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var capture = CreateCapture(session);
        return await _changeDetector.WaitForChangeAsync(capture, region, timeout, cancellationToken);
    }

    public async Task<ChangeResult> WaitForStabilityAsync(SessionHandle session, Rectangle region, TimeSpan stabilityDuration, CancellationToken cancellationToken = default)
    {
        var capture = CreateCapture(session);
        return await _changeDetector.WaitForStabilityAsync(capture, region, stabilityDuration, cancellationToken);
    }

    private ScreenCapture CreateCapture(SessionHandle session)
    {
        var captureLogger = _loggerFactory?.CreateLogger<ScreenCapture>();
        var capture = new ScreenCapture(session, _frameProvider, _options.DefaultCaptureOptions, captureLogger);
        return capture;
    }

    private void Cache(SessionHandle session, CaptureResult capture)
    {
        if (!_options.EnableCaching)
        {
            return;
        }

        _cache[session.SessionId] = capture;
        while (_cache.Count > _options.MaxCachedScreenshots)
        {
            var oldest = _cache.Keys.FirstOrDefault();
            if (oldest != Guid.Empty)
            {
                _cache.TryRemove(oldest, out _);
            }
        }
    }
}


