using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Processing;

public static class ImageFilters
{
    public static byte[] ApplyOcrFriendlyFilter(byte[] data)
        => new ImageProcessor().EnhanceForOcr(data);

    public static byte[] HighlightEdges(byte[] data)
    {
        using var image = Image.Load<Rgba32>(data);
        image.Mutate(ctx => ctx.DetectEdges());
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}


