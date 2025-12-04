using Cascade.Vision.Capture;

namespace Cascade.Vision.Comparison;

public interface IChangeDetector
{
    Task<ChangeResult> CompareAsync(byte[] baseline, byte[] current, CancellationToken cancellationToken = default);
    Task<ChangeResult> CompareAsync(CaptureResult baseline, CaptureResult current, CancellationToken cancellationToken = default);
    Task SetBaselineAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<ChangeResult> CompareWithBaselineAsync(byte[] current, CancellationToken cancellationToken = default);
    Task<ChangeResult> WaitForChangeAsync(Capture.IScreenCapture capture, Rectangle region, TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<ChangeResult> WaitForStabilityAsync(Capture.IScreenCapture capture, Rectangle region, TimeSpan stabilityDuration, CancellationToken cancellationToken = default);
}


