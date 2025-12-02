using System.Drawing;
using Cascade.UIAutomation.Elements;

namespace Cascade.Vision.Capture;

/// <summary>
/// Interface for screen capture operations.
/// </summary>
public interface IScreenCapture
{
    #region Full Screen Capture

    /// <summary>
    /// Captures a specific screen by index.
    /// </summary>
    /// <param name="screenIndex">The zero-based index of the screen to capture.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the screenshot.</returns>
    Task<CaptureResult> CaptureScreenAsync(int screenIndex = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures all screens as a single combined image.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the combined screenshot.</returns>
    Task<CaptureResult> CaptureAllScreensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the primary screen.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the screenshot.</returns>
    Task<CaptureResult> CapturePrimaryScreenAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Window Capture

    /// <summary>
    /// Captures a window by its handle.
    /// </summary>
    /// <param name="windowHandle">The window handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the window screenshot.</returns>
    Task<CaptureResult> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures a window by its title.
    /// </summary>
    /// <param name="windowTitle">The window title or partial title to match.</param>
    /// <param name="exactMatch">Whether to require an exact title match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the window screenshot.</returns>
    Task<CaptureResult> CaptureWindowAsync(string windowTitle, bool exactMatch = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the currently active foreground window.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the foreground window screenshot.</returns>
    Task<CaptureResult> CaptureForegroundWindowAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Region Capture

    /// <summary>
    /// Captures a specific screen region.
    /// </summary>
    /// <param name="region">The region to capture in screen coordinates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the region screenshot.</returns>
    Task<CaptureResult> CaptureRegionAsync(Rectangle region, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures a UI Automation element.
    /// </summary>
    /// <param name="element">The UI element to capture.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture result containing the element screenshot.</returns>
    Task<CaptureResult> CaptureElementAsync(IUIElement element, CancellationToken cancellationToken = default);

    #endregion

    #region Configuration

    /// <summary>
    /// Gets or sets the capture options.
    /// </summary>
    CaptureOptions Options { get; set; }

    #endregion
}

