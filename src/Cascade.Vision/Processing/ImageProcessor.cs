using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cascade.Vision.Processing;

public class ImageProcessor
{
    public byte[] Crop(byte[] imageData, Rectangle region)
        => Transform(imageData, ctx => ctx.Crop(region));

    public byte[] Resize(byte[] imageData, int width, int height, ResizeMode mode = ResizeMode.Fit)
        => Transform(imageData, ctx => ctx.Resize(new ResizeOptions { Size = new Size(width, height), Mode = mode }));

    public byte[] Rotate(byte[] imageData, double degrees)
        => Transform(imageData, ctx => ctx.Rotate((float)degrees));

    public byte[] Flip(byte[] imageData, FlipMode mode)
        => Transform(imageData, ctx => ctx.Flip(mode));

    public byte[] ToGrayscale(byte[] imageData)
        => Transform(imageData, ctx => ctx.Grayscale());

    public byte[] AdjustBrightness(byte[] imageData, float brightness)
        => Transform(imageData, ctx => ctx.Brightness(brightness));

    public byte[] AdjustContrast(byte[] imageData, float contrast)
        => Transform(imageData, ctx => ctx.Contrast(contrast));

    public byte[] Invert(byte[] imageData)
        => Transform(imageData, ctx => ctx.Invert());

    public byte[] Sharpen(byte[] imageData)
        => Transform(imageData, ctx => ctx.GaussianSharpen());

    public byte[] Denoise(byte[] imageData)
        => Transform(imageData, ctx => ctx.GaussianBlur(0.5f));

    public byte[] EnhanceForOcr(byte[] imageData)
        => Transform(imageData, ctx =>
        {
            ctx.Grayscale();
            ctx.Contrast(1.2f);
            ctx.GaussianSharpen(0.5f);
        });

    public byte[] Binarize(byte[] imageData, int threshold = 128)
        => Transform(imageData, ctx => ctx.BinaryThreshold(threshold / 255f));

    public byte[] AdaptiveThreshold(byte[] imageData)
        => Transform(imageData, ctx =>
        {
            ctx.GaussianBlur(1f);
            ctx.AdaptiveThreshold();
        });

    public Color GetDominantColor(byte[] imageData)
    {
        using var image = Image.Load<Rgba32>(imageData);
        var histogram = new Dictionary<Color, int>();
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var color = Color.FromArgb(row[x].A, row[x].R, row[x].G, row[x].B);
                    histogram[color] = histogram.TryGetValue(color, out var count) ? count + 1 : 1;
                }
            }
        });

        return histogram.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    public Color GetAverageColor(byte[] imageData, Rectangle? region = null)
    {
        using var image = Image.Load<Rgba32>(imageData);
        var area = region ?? new Rectangle(0, 0, image.Width, image.Height);
        double r = 0, g = 0, b = 0;
        var total = area.Width * area.Height;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = area.Top; y < area.Bottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = area.Left; x < area.Right; x++)
                {
                    r += row[x].R;
                    g += row[x].G;
                    b += row[x].B;
                }
            }
        });

        return Color.FromArgb(255, (int)(r / total), (int)(g / total), (int)(b / total));
    }

    public double GetBrightness(byte[] imageData)
        => new Analysis.ContrastAnalyzer().GetBrightness(imageData);

    public double GetContrast(byte[] imageData)
        => new Analysis.ContrastAnalyzer().GetContrast(imageData);

    private static byte[] Transform(byte[] imageData, Action<IImageProcessingContext> mutation)
    {
        using var image = Image.Load<Rgba32>(imageData);
        image.Mutate(mutation);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}


