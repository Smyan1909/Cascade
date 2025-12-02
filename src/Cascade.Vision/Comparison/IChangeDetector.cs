using System.Drawing;
using Cascade.Vision.Capture;

namespace Cascade.Vision.Comparison;

/// <summary>
/// Interface for detecting visual changes between images.
/// </summary>
public interface IChangeDetector
{
    #region Direct Comparison

    /// <summary>
    /// Compares two images and returns the differences.
    /// </summary>
    /// <param name="baseline">The baseline image data.</param>
    /// <param name="current">The current image data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The change result.</returns>
    Task<ChangeResult> CompareAsync(byte[] baseline, byte[] current, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two capture results.
    /// </summary>
    /// <param name="baseline">The baseline capture.</param>
    /// <param name="current">The current capture.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The change result.</returns>
    Task<ChangeResult> CompareAsync(CaptureResult baseline, CaptureResult current, CancellationToken cancellationToken = default);

    #endregion

    #region Baseline Management

    /// <summary>
    /// Sets the baseline image for subsequent comparisons.
    /// </summary>
    /// <param name="imageData">The baseline image data.</param>
    Task SetBaselineAsync(byte[] imageData);

    /// <summary>
    /// Sets the baseline from a capture result.
    /// </summary>
    /// <param name="capture">The baseline capture.</param>
    Task SetBaselineAsync(CaptureResult capture);

    /// <summary>
    /// Compares an image with the stored baseline.
    /// </summary>
    /// <param name="current">The current image data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The change result.</returns>
    Task<ChangeResult> CompareWithBaselineAsync(byte[] current, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a baseline is currently set.
    /// </summary>
    bool HasBaseline { get; }

    /// <summary>
    /// Clears the stored baseline.
    /// </summary>
    void ClearBaseline();

    #endregion

    #region Monitoring

    /// <summary>
    /// Waits for a change to occur in the specified region.
    /// </summary>
    /// <param name="capture">The screen capture service to use.</param>
    /// <param name="region">The region to monitor.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The change result when a change is detected, or no-change if timeout.</returns>
    Task<ChangeResult> WaitForChangeAsync(
        IScreenCapture capture,
        Rectangle region,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the UI to stabilize (no changes for the specified duration).
    /// </summary>
    /// <param name="capture">The screen capture service to use.</param>
    /// <param name="region">The region to monitor.</param>
    /// <param name="stabilityDuration">Duration of stability required.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final change result.</returns>
    Task<ChangeResult> WaitForStabilityAsync(
        IScreenCapture capture,
        Rectangle region,
        TimeSpan stabilityDuration,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    #endregion

    #region Configuration

    /// <summary>
    /// Gets or sets the comparison options.
    /// </summary>
    ComparisonOptions Options { get; set; }

    #endregion
}

