namespace Cascade.Vision.Processing;

public static class ImageFilters
{
    public static byte[] ApplyOcrFriendlyFilter(byte[] data)
        => new ImageProcessor().EnhanceForOcr(data);

    public static byte[] HighlightEdges(byte[] data)
    {
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(data);
        image.Mutate(ctx => ctx.DetectEdges());
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}


