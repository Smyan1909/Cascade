using System.Threading.Tasks;
using Cascade.Core.Session;
using Cascade.Tests.Vision;
using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Vision.Comparison;

public class ChangeDetectorTests
{
    [Fact]
    public async Task CompareAsync_DetectsPixelDifferences()
    {
        var detector = new ChangeDetector(new ComparisonOptions { ChangeThreshold = 0.01 });
        var baseline = TestImageFactory.CreateSolidColor(System.Drawing.Color.White, 20, 20);
        var current = TestImageFactory.CreateSolidColor(System.Drawing.Color.Black, 20, 20);

        var result = await detector.CompareAsync(baseline, current);

        result.HasChanges.Should().BeTrue();
        result.DifferencePercentage.Should().BeGreaterThan(0.9);
        result.ChangedRegions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CompareAsync_WithCaptureResults_Works()
    {
        var detector = new ChangeDetector();
        var session = new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() };
        var baseline = new CaptureResult { SessionId = session.SessionId, ImageData = TestImageFactory.CreateSolidColor(System.Drawing.Color.Blue, 10, 10) };
        var next = new CaptureResult { SessionId = session.SessionId, ImageData = TestImageFactory.CreateSolidColor(System.Drawing.Color.Blue, 10, 10) };

        var result = await detector.CompareAsync(baseline, next);

        result.HasChanges.Should().BeFalse();
    }
}


