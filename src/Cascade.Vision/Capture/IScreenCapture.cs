namespace Cascade.Vision.Capture;

public interface IScreenCapture
{
    SessionHandle Session { get; }

    Task<CaptureResult> CaptureScreenAsync(int screenIndex = 0, CancellationToken cancellationToken = default);
    Task<CaptureResult> CaptureAllScreensAsync(CancellationToken cancellationToken = default);
    Task<CaptureResult> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken = default);
    Task<CaptureResult> CaptureWindowAsync(string windowTitle, CancellationToken cancellationToken = default);
    Task<CaptureResult> CaptureForegroundWindowAsync(CancellationToken cancellationToken = default);
    Task<CaptureResult> CaptureRegionAsync(Rectangle region, CancellationToken cancellationToken = default);
    Task<CaptureResult> CaptureElementAsync(IUIElement element, CancellationToken cancellationToken = default);
    Task<CaptureResult> CaptureInteractiveAsync(CancellationToken cancellationToken = default);

    CaptureOptions Options { get; set; }
}


