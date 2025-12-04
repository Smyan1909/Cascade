using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;

namespace Cascade.Vision.Capture;

public sealed class ScreenCapture : IScreenCapture
{
    private readonly ISessionFrameProvider _frameProvider;
    private readonly ILogger<ScreenCapture>? _logger;

    public ScreenCapture(SessionHandle session, ISessionFrameProvider frameProvider, CaptureOptions? options = null, ILogger<ScreenCapture>? logger = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
        Options = options ?? new CaptureOptions();
        _logger = logger;
    }

    public SessionHandle Session { get; }
    public CaptureOptions Options { get; set; }

    public Task<CaptureResult> CaptureScreenAsync(int screenIndex = 0, CancellationToken cancellationToken = default)
        => CaptureAsync(() => _frameProvider.CaptureScreenAsync(Session, screenIndex, Options, cancellationToken), cancellationToken);

    public Task<CaptureResult> CaptureAllScreensAsync(CancellationToken cancellationToken = default)
        => CaptureAsync(() => _frameProvider.CaptureAllScreensAsync(Session, Options, cancellationToken), cancellationToken);

    public Task<CaptureResult> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
        => CaptureAsync(() => _frameProvider.CaptureWindowAsync(Session, windowHandle, Options, cancellationToken), cancellationToken, windowHandle);

    public Task<CaptureResult> CaptureWindowAsync(string windowTitle, CancellationToken cancellationToken = default)
        => CaptureAsync(() => _frameProvider.CaptureWindowAsync(Session, windowTitle, Options, cancellationToken), cancellationToken);

    public Task<CaptureResult> CaptureForegroundWindowAsync(CancellationToken cancellationToken = default)
        => CaptureAsync(() => _frameProvider.CaptureForegroundWindowAsync(Session, Options, cancellationToken), cancellationToken);

    public Task<CaptureResult> CaptureRegionAsync(Rectangle region, CancellationToken cancellationToken = default)
        => CaptureAsync(() => _frameProvider.CaptureRegionAsync(Session, region, Options, cancellationToken), cancellationToken);

    public Task<CaptureResult> CaptureElementAsync(IUIElement element, CancellationToken cancellationToken = default)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        var region = element.BoundingRectangle;
        return CaptureRegionAsync(region, cancellationToken);
    }

    public Task<CaptureResult> CaptureInteractiveAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Interactive capture requires a UI loop and is not available in headless mode.");

    private async Task<CaptureResult> CaptureAsync(Func<Task<Bitmap>> capture, CancellationToken cancellationToken, IntPtr? windowHandle = null)
    {
        Session.EnsureValid();
        using var bitmap = await capture().ConfigureAwait(false);
        var region = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = Encode(bitmap, Options.ImageFormat, Options.JpegQuality);
        _logger?.LogDebug("Captured frame {Width}x{Height} ({Format}) for session {SessionId}", bitmap.Width, bitmap.Height, Options.ImageFormat, Session.SessionId);

        return new CaptureResult
        {
            SessionId = Session.SessionId,
            ImageData = data,
            ImageFormat = Options.ImageFormat,
            Width = bitmap.Width,
            Height = bitmap.Height,
            CapturedRegion = region,
            CapturedAt = DateTime.UtcNow,
            SourceWindowHandle = windowHandle
        };
    }

    private static byte[] Encode(Bitmap bitmap, string format, int jpegQuality)
    {
        var imageFormat = format.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ImageFormat.Jpeg,
            "bmp" => ImageFormat.Bmp,
            "gif" => ImageFormat.Gif,
            _ => ImageFormat.Png
        };

        using var stream = new MemoryStream();
        if (imageFormat.Equals(ImageFormat.Jpeg))
        {
            var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var quality = Math.Clamp(jpegQuality, 10, 100);
            var qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = qualityParam;

            if (encoder is not null)
            {
                bitmap.Save(stream, encoder, encoderParams);
                return stream.ToArray();
            }
        }

        bitmap.Save(stream, imageFormat);
        return stream.ToArray();
    }
}


