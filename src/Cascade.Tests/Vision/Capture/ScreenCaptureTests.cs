using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Automation;
using Cascade.Core.Session;
using Cascade.UIAutomation.Session;
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

    [Fact]
    public async Task CaptureWindowAsync_SetsSourceHandle()
    {
        var session = new SessionHandle
        {
            SessionId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            VirtualDesktopId = new IntPtr(1)
        };

        var provider = new TrackingFrameProvider();
        var capture = new ScreenCapture(session, provider);
        var handle = new IntPtr(1234);

        var result = await capture.CaptureWindowAsync(handle);

        result.SourceWindowHandle.Should().Be(handle);
        provider.LastWindowHandle.Should().Be(handle);
    }

    [Fact]
    public async Task CaptureElementAsync_UsesElementBoundingRectangle()
    {
        var session = new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() };
        var provider = new TrackingFrameProvider();
        var capture = new ScreenCapture(session, provider);
        var element = new FakeElement(new Rectangle(0, 0, 15, 25));

        var result = await capture.CaptureElementAsync(element);

        result.Width.Should().Be(15);
        result.Height.Should().Be(25);
        provider.LastCapturedRegion.Should().Be(new Rectangle(0, 0, 15, 25));
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

    private sealed class TrackingFrameProvider : ISessionFrameProvider
    {
        public Rectangle LastCapturedRegion { get; private set; } = Rectangle.Empty;
        public IntPtr? LastWindowHandle { get; private set; }

        public Task<Bitmap> CaptureRegionAsync(SessionHandle session, Rectangle region, CaptureOptions options, CancellationToken cancellationToken = default)
        {
            LastCapturedRegion = region;
            return Task.FromResult(new Bitmap(region.Width, region.Height));
        }

        public Task<Bitmap> CaptureAllScreensAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(2, 2));

        public Task<Bitmap> CaptureForegroundWindowAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(3, 3));

        public Task<Bitmap> CaptureScreenAsync(SessionHandle session, int screenIndex, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(4, 4));

        public Task<Bitmap> CaptureWindowAsync(SessionHandle session, IntPtr windowHandle, CaptureOptions options, CancellationToken cancellationToken = default)
        {
            LastWindowHandle = windowHandle;
            return Task.FromResult(new Bitmap(1, 1));
        }

        public Task<Bitmap> CaptureWindowAsync(SessionHandle session, string windowTitle, CaptureOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Bitmap(1, 1));
    }

    private sealed class FakeElement : Cascade.UIAutomation.Elements.IUIElement
    {
        public FakeElement(Rectangle rect)
        {
            BoundingRectangle = rect;
        }

        public SessionHandle Session => new();
        public VirtualInputChannel InputChannel => new() { Profile = VirtualInputProfile.Balanced };
        public string AutomationId => string.Empty;
        public string Name => string.Empty;
        public string ClassName => string.Empty;
        public ControlType ControlType => ControlType.Custom;
        public string RuntimeId => string.Empty;
        public int ProcessId => 0;
        public Cascade.UIAutomation.Elements.IUIElement? Parent => null;
        public IReadOnlyList<Cascade.UIAutomation.Elements.IUIElement> Children => Array.Empty<Cascade.UIAutomation.Elements.IUIElement>();
        public Rectangle BoundingRectangle { get; }
        public Point ClickablePoint => Point.Empty;
        public bool IsOffscreen => false;
        public bool IsEnabled => true;
        public bool HasKeyboardFocus => true;
        public bool IsContentElement => true;
        public bool IsControlElement => true;
        public IReadOnlyList<Cascade.UIAutomation.Patterns.PatternType> SupportedPatterns => Array.Empty<Cascade.UIAutomation.Patterns.PatternType>();

        public Task ClickAsync(Cascade.UIAutomation.Actions.ClickType clickType = Cascade.UIAutomation.Actions.ClickType.Left) => Task.CompletedTask;
        public Task DoubleClickAsync() => Task.CompletedTask;
        public Task RightClickAsync() => Task.CompletedTask;
        public Task TypeTextAsync(string text) => Task.CompletedTask;
        public Task SetValueAsync(string value) => Task.CompletedTask;
        public Task InvokeAsync() => Task.CompletedTask;
        public Task SetFocusAsync() => Task.CompletedTask;
        public Cascade.UIAutomation.Elements.IUIElement? FindFirst(Cascade.UIAutomation.Discovery.SearchCriteria criteria) => null;
        public IReadOnlyList<Cascade.UIAutomation.Elements.IUIElement> FindAll(Cascade.UIAutomation.Discovery.SearchCriteria criteria) => Array.Empty<Cascade.UIAutomation.Elements.IUIElement>();
        public bool TryGetPattern<T>(out T pattern) where T : class { pattern = null!; return false; }
        public Cascade.UIAutomation.Models.ElementSnapshot ToSnapshot() => new();
    }
}


