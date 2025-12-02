using System.Drawing;
using System.Drawing.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Bmp;
using SharpImage = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Capture;

/// <summary>
/// Represents the result of a screen capture operation.
/// </summary>
public class CaptureResult : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the raw image data as bytes.
    /// </summary>
    public byte[] ImageData { get; }

    /// <summary>
    /// Gets the image format ("png", "jpeg", "bmp").
    /// </summary>
    public string ImageFormat { get; }

    /// <summary>
    /// Gets the width of the captured image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the captured image in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the screen region that was captured.
    /// </summary>
    public System.Drawing.Rectangle CapturedRegion { get; }

    /// <summary>
    /// Gets the timestamp when the capture was taken.
    /// </summary>
    public DateTime CapturedAt { get; }

    /// <summary>
    /// Gets the window handle if the capture was of a specific window.
    /// </summary>
    public IntPtr? SourceWindowHandle { get; }

    /// <summary>
    /// Gets the title of the source window if available.
    /// </summary>
    public string? SourceWindowTitle { get; init; }

    /// <summary>
    /// Creates a new CaptureResult.
    /// </summary>
    public CaptureResult(
        byte[] imageData,
        string imageFormat,
        int width,
        int height,
        System.Drawing.Rectangle capturedRegion,
        IntPtr? sourceWindowHandle = null)
    {
        ImageData = imageData ?? throw new ArgumentNullException(nameof(imageData));
        ImageFormat = imageFormat ?? throw new ArgumentNullException(nameof(imageFormat));
        Width = width;
        Height = height;
        CapturedRegion = capturedRegion;
        CapturedAt = DateTime.UtcNow;
        SourceWindowHandle = sourceWindowHandle;
    }

    /// <summary>
    /// Converts the captured image to an ImageSharp Image.
    /// </summary>
    public Image<Rgba32> ToImage()
    {
        return SharpImage.Load<Rgba32>(ImageData);
    }

    /// <summary>
    /// Converts the captured image to a System.Drawing.Bitmap.
    /// </summary>
    public Bitmap ToBitmap()
    {
        using var ms = new MemoryStream(ImageData);
        return new Bitmap(ms);
    }

    /// <summary>
    /// Converts the image data to a Base64 string.
    /// </summary>
    public string ToBase64()
    {
        return Convert.ToBase64String(ImageData);
    }

    /// <summary>
    /// Gets a Base64 data URL suitable for HTML img src.
    /// </summary>
    public string ToDataUrl()
    {
        var mimeType = ImageFormat.ToLowerInvariant() switch
        {
            "png" => "image/png",
            "jpeg" or "jpg" => "image/jpeg",
            "bmp" => "image/bmp",
            _ => "image/png"
        };
        return $"data:{mimeType};base64,{ToBase64()}";
    }

    /// <summary>
    /// Saves the captured image to a file.
    /// </summary>
    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await File.WriteAllBytesAsync(filePath, ImageData, cancellationToken);
    }

    /// <summary>
    /// Saves the captured image to a file synchronously.
    /// </summary>
    public void Save(string filePath)
    {
        File.WriteAllBytes(filePath, ImageData);
    }

    /// <summary>
    /// Gets the image data as a stream.
    /// </summary>
    public Stream ToStream()
    {
        return new MemoryStream(ImageData);
    }

    /// <summary>
    /// Gets the image data as a stream asynchronously.
    /// </summary>
    public Task<Stream> ToStreamAsync()
    {
        return Task.FromResult<Stream>(new MemoryStream(ImageData));
    }

    /// <summary>
    /// Creates a new CaptureResult with a cropped region.
    /// </summary>
    public CaptureResult Crop(System.Drawing.Rectangle cropRegion)
    {
        using var image = ToImage();
        image.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(
            cropRegion.X, cropRegion.Y, cropRegion.Width, cropRegion.Height)));
        
        using var ms = new MemoryStream();
        var encoder = GetEncoder();
        image.Save(ms, encoder);
        
        return new CaptureResult(
            ms.ToArray(),
            ImageFormat,
            cropRegion.Width,
            cropRegion.Height,
            new System.Drawing.Rectangle(
                CapturedRegion.X + cropRegion.X,
                CapturedRegion.Y + cropRegion.Y,
                cropRegion.Width,
                cropRegion.Height),
            SourceWindowHandle)
        {
            SourceWindowTitle = SourceWindowTitle
        };
    }

    private SixLabors.ImageSharp.Formats.IImageEncoder GetEncoder()
    {
        return ImageFormat.ToLowerInvariant() switch
        {
            "png" => new PngEncoder(),
            "jpeg" or "jpg" => new JpegEncoder { Quality = 90 },
            "bmp" => new BmpEncoder(),
            _ => new PngEncoder()
        };
    }

    /// <summary>
    /// Disposes of any resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

