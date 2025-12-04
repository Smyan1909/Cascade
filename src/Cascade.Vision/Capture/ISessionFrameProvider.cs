namespace Cascade.Vision.Capture;

public interface ISessionFrameProvider
{
    Task<Bitmap> CaptureScreenAsync(SessionHandle session, int screenIndex, CaptureOptions options, CancellationToken cancellationToken = default);
    Task<Bitmap> CaptureAllScreensAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default);
    Task<Bitmap> CaptureWindowAsync(SessionHandle session, IntPtr windowHandle, CaptureOptions options, CancellationToken cancellationToken = default);
    Task<Bitmap> CaptureWindowAsync(SessionHandle session, string windowTitle, CaptureOptions options, CancellationToken cancellationToken = default);
    Task<Bitmap> CaptureForegroundWindowAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default);
    Task<Bitmap> CaptureRegionAsync(SessionHandle session, Rectangle region, CaptureOptions options, CancellationToken cancellationToken = default);
}


