using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Cascade.UIAutomation.Elements;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Capture;

/// <summary>
/// Screen capture implementation using Win32 APIs.
/// </summary>
public class ScreenCapture : IScreenCapture
{
    #region Win32 Imports

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSource, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public System.Drawing.Rectangle ToRectangle() =>
            new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int SRCCOPY = 0x00CC0020;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const uint PW_CLIENTONLY = 0x1;
    private const uint PW_RENDERFULLCONTENT = 0x2;

    #endregion

    /// <summary>
    /// Gets or sets the capture options.
    /// </summary>
    public CaptureOptions Options { get; set; } = new();

    /// <inheritdoc />
    public Task<CaptureResult> CaptureScreenAsync(int screenIndex = 0, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var bounds = RegionSelector.GetScreenBounds(screenIndex);
            if (bounds.IsEmpty)
                throw new ArgumentException($"Screen index {screenIndex} is not valid.", nameof(screenIndex));

            return CaptureRegionCore(bounds, null);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CaptureResult> CaptureAllScreensAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var bounds = RegionSelector.GetVirtualScreenBounds();
            return CaptureRegionCore(bounds, null);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CaptureResult> CapturePrimaryScreenAsync(CancellationToken cancellationToken = default)
    {
        return CaptureScreenAsync(0, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CaptureResult> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CaptureWindowCore(windowHandle), cancellationToken);
    }

    /// <inheritdoc />
    public Task<CaptureResult> CaptureWindowAsync(string windowTitle, bool exactMatch = false, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var handle = FindWindowByTitle(windowTitle, exactMatch);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"Window with title '{windowTitle}' not found.");

            return CaptureWindowCore(handle);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CaptureResult> CaptureForegroundWindowAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("No foreground window found.");

            return CaptureWindowCore(handle);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CaptureResult> CaptureRegionAsync(System.Drawing.Rectangle region, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CaptureRegionCore(region, null), cancellationToken);
    }

    /// <inheritdoc />
    public Task<CaptureResult> CaptureElementAsync(IUIElement element, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var bounds = element.BoundingRectangle;
            if (bounds.IsEmpty)
                throw new InvalidOperationException("Element has no bounding rectangle.");

            return CaptureRegionCore(bounds, null);
        }, cancellationToken);
    }

    private CaptureResult CaptureWindowCore(IntPtr windowHandle)
    {
        var windowTitle = GetWindowTitle(windowHandle);
        System.Drawing.Rectangle bounds;

        // Try to get extended frame bounds (more accurate on Windows 10+)
        if (DwmGetWindowAttribute(windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS,
            out RECT extendedBounds, Marshal.SizeOf<RECT>()) == 0)
        {
            bounds = extendedBounds.ToRectangle();
        }
        else
        {
            GetWindowRect(windowHandle, out RECT windowRect);
            bounds = windowRect.ToRectangle();
        }

        if (Options.ClientAreaOnly)
        {
            GetClientRect(windowHandle, out RECT clientRect);
            POINT topLeft = new() { X = 0, Y = 0 };
            ClientToScreen(windowHandle, ref topLeft);
            bounds = new System.Drawing.Rectangle(
                topLeft.X, topLeft.Y,
                clientRect.Right - clientRect.Left,
                clientRect.Bottom - clientRect.Top);
        }

        // Use PrintWindow for better capture of layered/transparent windows
        using var bitmap = CaptureWindowBitmap(windowHandle, bounds.Width, bounds.Height);
        var imageData = BitmapToBytes(bitmap);

        return new CaptureResult(
            imageData,
            Options.ImageFormat,
            bounds.Width,
            bounds.Height,
            bounds,
            windowHandle)
        {
            SourceWindowTitle = windowTitle
        };
    }

    private Bitmap CaptureWindowBitmap(IntPtr windowHandle, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var hdc = graphics.GetHdc();
            try
            {
                // Try PrintWindow first (works better for modern apps)
                uint flags = Options.ClientAreaOnly ? PW_CLIENTONLY : PW_RENDERFULLCONTENT;
                if (!PrintWindow(windowHandle, hdc, flags))
                {
                    // Fallback to BitBlt
                    var windowDc = GetWindowDC(windowHandle);
                    try
                    {
                        BitBlt(hdc, 0, 0, width, height, windowDc, 0, 0, SRCCOPY);
                    }
                    finally
                    {
                        ReleaseDC(windowHandle, windowDc);
                    }
                }
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        return bitmap;
    }

    private CaptureResult CaptureRegionCore(System.Drawing.Rectangle region, IntPtr? windowHandle)
    {
        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                region.Left, region.Top,
                0, 0,
                region.Size,
                CopyPixelOperation.SourceCopy);

            if (Options.IncludeCursor)
            {
                DrawCursor(graphics, region);
            }
        }

        var processedBitmap = ProcessBitmap(bitmap);
        var imageData = BitmapToBytes(processedBitmap);

        if (processedBitmap != bitmap)
            processedBitmap.Dispose();

        return new CaptureResult(
            imageData,
            Options.ImageFormat,
            (int)(region.Width * Options.Scale),
            (int)(region.Height * Options.Scale),
            region,
            windowHandle);
    }

    private Bitmap ProcessBitmap(Bitmap original)
    {
        if (Math.Abs(Options.Scale - 1.0) < 0.001 && !Options.RemoveTransparency)
            return original;

        int newWidth = (int)(original.Width * Options.Scale);
        int newHeight = (int)(original.Height * Options.Scale);

        var processed = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        
        using (var graphics = Graphics.FromImage(processed))
        {
            if (Options.RemoveTransparency)
            {
                graphics.Clear(Options.TransparencyReplacement);
            }

            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.DrawImage(original, 0, 0, newWidth, newHeight);
        }

        return processed;
    }

    private void DrawCursor(Graphics graphics, System.Drawing.Rectangle captureRegion)
    {
        try
        {
            var cursorInfo = new CursorInfo { cbSize = Marshal.SizeOf<CursorInfo>() };
            if (GetCursorInfo(ref cursorInfo) && cursorInfo.flags == 1) // CURSOR_SHOWING
            {
                var cursorPos = cursorInfo.ptScreenPos;
                if (captureRegion.Contains(cursorPos.X, cursorPos.Y))
                {
                    Cursors.Default.Draw(graphics,
                        new System.Drawing.Rectangle(
                            cursorPos.X - captureRegion.Left,
                            cursorPos.Y - captureRegion.Top,
                            32, 32));
                }
            }
        }
        catch
        {
            // Ignore cursor drawing errors
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CursorInfo pci);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    private byte[] BitmapToBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        using var image = ConvertToImageSharp(bitmap);
        
        var encoder = Options.ImageFormat.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => (SixLabors.ImageSharp.Formats.IImageEncoder)new JpegEncoder { Quality = Options.JpegQuality },
            "bmp" => new BmpEncoder(),
            _ => new PngEncoder()
        };

        image.Save(ms, encoder);
        return ms.ToArray();
    }

    private static Image<Rgba32> ConvertToImageSharp(Bitmap bitmap)
    {
        var bitmapData = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var image = new Image<Rgba32>(bitmap.Width, bitmap.Height);
            
            unsafe
            {
                byte* sourcePtr = (byte*)bitmapData.Scan0;
                
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        byte* sourceRow = sourcePtr + (y * bitmapData.Stride);
                        
                        for (int x = 0; x < accessor.Width; x++)
                        {
                            // BGRA to RGBA
                            row[x] = new Rgba32(
                                sourceRow[x * 4 + 2], // R
                                sourceRow[x * 4 + 1], // G
                                sourceRow[x * 4 + 0], // B
                                sourceRow[x * 4 + 3]  // A
                            );
                        }
                    }
                });
            }

            return image;
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    private static IntPtr FindWindowByTitle(string title, bool exactMatch)
    {
        IntPtr foundHandle = IntPtr.Zero;
        
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var windowTitle = GetWindowTitle(hWnd);
            if (string.IsNullOrEmpty(windowTitle))
                return true;

            bool matches = exactMatch
                ? windowTitle.Equals(title, StringComparison.OrdinalIgnoreCase)
                : windowTitle.Contains(title, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                foundHandle = hWnd;
                return false; // Stop enumeration
            }

            return true;
        }, IntPtr.Zero);

        return foundHandle;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        var builder = new System.Text.StringBuilder(length + 1);
        GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }
}

