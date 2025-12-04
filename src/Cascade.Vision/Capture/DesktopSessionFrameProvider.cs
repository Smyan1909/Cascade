using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Cascade.Vision.Capture;

/// <summary>
/// Default frame provider that mirrors the visible desktop. In production this is replaced with a hidden-desktop duplicator.
/// </summary>
public sealed class DesktopSessionFrameProvider : ISessionFrameProvider
{
    public Task<Bitmap> CaptureScreenAsync(SessionHandle session, int screenIndex, CaptureOptions options, CancellationToken cancellationToken = default)
    {
        session.EnsureValid();
        var screens = Screen.AllScreens;
        screenIndex = Math.Clamp(screenIndex, 0, screens.Length - 1);
        return Task.FromResult(CaptureRegionInternal(screens[screenIndex].Bounds, options));
    }

    public Task<Bitmap> CaptureAllScreensAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default)
    {
        session.EnsureValid();
        var bounds = Screen.AllScreens.Select(screen => screen.Bounds).Aggregate(Rectangle.Union);
        return Task.FromResult(CaptureRegionInternal(bounds, options));
    }

    public Task<Bitmap> CaptureWindowAsync(SessionHandle session, IntPtr windowHandle, CaptureOptions options, CancellationToken cancellationToken = default)
    {
        session.EnsureValid();
        var rect = NativeMethods.GetWindowRectangle(windowHandle);
        return Task.FromResult(CaptureRegionInternal(rect, options));
    }

    public Task<Bitmap> CaptureWindowAsync(SessionHandle session, string windowTitle, CaptureOptions options, CancellationToken cancellationToken = default)
    {
        session.EnsureValid();
        var handle = NativeMethods.FindWindow(windowTitle);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Window '{windowTitle}' not found.");
        }

        return CaptureWindowAsync(session, handle, options, cancellationToken);
    }

    public Task<Bitmap> CaptureForegroundWindowAsync(SessionHandle session, CaptureOptions options, CancellationToken cancellationToken = default)
    {
        session.EnsureValid();
        var handle = NativeMethods.GetForegroundWindow();
        return CaptureWindowAsync(session, handle, options, cancellationToken);
    }

    public Task<Bitmap> CaptureRegionAsync(SessionHandle session, Rectangle region, CaptureOptions options, CancellationToken cancellationToken = default)
    {
        session.EnsureValid();
        return Task.FromResult(CaptureRegionInternal(region, options));
    }

    private static Bitmap CaptureRegionInternal(Rectangle region, CaptureOptions options)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentException("Capture region must have positive size.", nameof(region));
        }

        region = ApplyCrop(region, options);
        using var bitmap = new Bitmap(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
        }

        if (!options.RemoveTransparency)
        {
            return ApplyScale(bitmap, options.Scale);
        }

        using var composited = new Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(composited))
        {
            graphics.Clear(options.TransparencyReplacement);
            graphics.DrawImage(bitmap, Point.Empty);
        }

        return ApplyScale(composited, options.Scale);
    }

    private static Rectangle ApplyCrop(Rectangle region, CaptureOptions options)
    {
        if (options.CropRegion is null || options.CropRegion == Rectangle.Empty)
        {
            return region;
        }

        return RegionSelector.ClampToBounds(options.CropRegion.Value, region);
    }

    private static Bitmap ApplyScale(Image image, double scale)
    {
        if (scale <= 0 || Math.Abs(scale - 1.0) < 0.01)
        {
            return new Bitmap(image);
        }

        var width = Math.Max(1, (int)(image.Width * scale));
        var height = Math.Max(1, (int)(image.Height * scale));
        var output = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(output);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(image, new Rectangle(0, 0, width, height));
        return output;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        public static Rectangle GetWindowRectangle(IntPtr handle)
        {
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect))
            {
                return Rectangle.Empty;
            }

            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static IntPtr FindWindow(string windowTitle) => FindWindow(null, windowTitle);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}


