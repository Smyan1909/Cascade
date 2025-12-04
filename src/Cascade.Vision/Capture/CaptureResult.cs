using System.Drawing.Imaging;

namespace Cascade.Vision.Capture;

public sealed class CaptureResult
{
    public Guid SessionId { get; init; }
    public byte[] ImageData { get; init; } = Array.Empty<byte>();
    public string ImageFormat { get; init; } = "png";
    public int Width { get; init; }
    public int Height { get; init; }
    public Rectangle CapturedRegion { get; init; }
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    public IntPtr? SourceWindowHandle { get; init; }

    public Image ToImage()
    {
        var stream = new MemoryStream(ImageData, writable: false);
        return Image.FromStream(stream);
    }

    public Bitmap ToBitmap()
    {
        using var image = ToImage();
        return new Bitmap(image);
    }

    public string ToBase64() => Convert.ToBase64String(ImageData);

    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(ImageData.AsMemory(0, ImageData.Length), cancellationToken);
    }

    public Task<Stream> ToStreamAsync()
    {
        Stream stream = new MemoryStream(ImageData, writable: false);
        return Task.FromResult(stream);
    }
}


