using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;
using Color = System.Drawing.Color;

namespace Cascade.Vision.Analysis;

public class ContrastAnalyzer
{
    public double GetBrightness(byte[] imageData)
    {
        using var image = Image.Load<Rgba32>(imageData);
        double total = 0;
        var pixels = image.Width * image.Height;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    total += (0.299 * row[x].R + 0.587 * row[x].G + 0.114 * row[x].B) / 255d;
                }
            }
        });

        return total / pixels;
    }

    public double GetContrast(byte[] imageData)
    {
        using var image = Image.Load<Rgba32>(imageData);
        double mean = GetBrightness(imageData);
        double variance = 0;
        var pixels = image.Width * image.Height;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var brightness = (0.299 * row[x].R + 0.587 * row[x].G + 0.114 * row[x].B) / 255d;
                    variance += Math.Pow(brightness - mean, 2);
                }
            }
        });

        return Math.Sqrt(variance / pixels);
    }

    public static double GetContrastRatio(Color foreground, Color background)
    {
        var l1 = RelativeLuminance(foreground);
        var l2 = RelativeLuminance(background);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        double Linearize(byte component)
        {
            var channel = component / 255d;
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(color.R) +
               0.7152 * Linearize(color.G) +
               0.0722 * Linearize(color.B);
    }
}


