using System.Drawing;
using System.Threading.Tasks;
using Cascade.Core.Session;
using Cascade.Vision.Capture;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Vision.Capture;

public class ScreenCaptureTests
{
    [Fact]
    public async Task CaptureRegionAsync_ReturnsMetadataAndEncodesImage()
    {
        var session = new SessionHandle
        {
            SessionId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            VirtualDesktopId = new IntPtr(1),
            UserProfilePath = "test"
        };

        var options = new CaptureOptions { ImageFormat = "png" };
        var provider = new FakeFrameProvider();
        var capture = new ScreenCapture(session, provider, options);

        var region = new Rectangle(0, 0, 40, 20);
        var result = await capture.CaptureRegionAsync(region);

        result.SessionId.Should().Be(session.SessionId);
        result.ImageData.Should().NotBeNullOrEmpty();
        result.Width.Should().Be(40);
        result.Height.Should().Be(20);
        result.ImageFormat.Should().Be("png");
    }

    private sealed class FakeFrameProvider : ISessionFrameProvider
    {
        public Task<Bitmap> CaptureRegionAsync(SessionHandle session, Rectangle region, CaptureOptions options, CancellationToken cancellationToken = default)
        {
            var bitmap = new Bitmap(region.Width, region.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.AliceBlue);
            return Task.FromResult(bitmap);
        }

        public Task<Bitmap> CaptureAllScreensAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(1, 1));

        public Task<Bitmap> CaptureForegroundWindowAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(1, 1));

        public Task<Bitmap> CaptureScreenAsync(SessionHandle session, int screenIndex, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(1, 1));

        public Task<Bitmap> CaptureWindowAsync(SessionHandle session, IntPtr windowHandle, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(1, 1));

        public Task<Bitmap> CaptureWindowAsync(SessionHandle session, string windowTitle, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(1, 1));
    }
}


