using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SharpImage = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Processing;

/// <summary>
/// Provides image manipulation operations for OCR preprocessing and analysis.
/// </summary>
public class ImageProcessor
{
    #region Basic Operations

    /// <summary>
    /// Crops an image to the specified region.
    /// </summary>
    public byte[] Crop(byte[] imageData, System.Drawing.Rectangle region)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(
            region.X, region.Y, region.Width, region.Height)));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    public byte[] Resize(byte[] imageData, int width, int height, ResizeMode mode = ResizeMode.Fit)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        var resizeOptions = new SixLabors.ImageSharp.Processing.ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(width, height),
            Mode = mode switch
            {
                ResizeMode.Stretch => SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
                ResizeMode.Fit => SixLabors.ImageSharp.Processing.ResizeMode.Max,
                ResizeMode.Fill => SixLabors.ImageSharp.Processing.ResizeMode.Min,
                ResizeMode.Pad => SixLabors.ImageSharp.Processing.ResizeMode.Pad,
                _ => SixLabors.ImageSharp.Processing.ResizeMode.Max
            }
        };
        image.Mutate(ctx => ctx.Resize(resizeOptions));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Scales an image by a factor.
    /// </summary>
    public byte[] Scale(byte[] imageData, double factor)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        int newWidth = (int)(image.Width * factor);
        int newHeight = (int)(image.Height * factor);
        image.Mutate(ctx => ctx.Resize(newWidth, newHeight));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Rotates an image by the specified degrees.
    /// </summary>
    public byte[] Rotate(byte[] imageData, float degrees)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Rotate(degrees));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Flips an image.
    /// </summary>
    public byte[] Flip(byte[] imageData, FlipMode mode)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx =>
        {
            switch (mode)
            {
                case FlipMode.Horizontal:
                    ctx.Flip(SixLabors.ImageSharp.Processing.FlipMode.Horizontal);
                    break;
                case FlipMode.Vertical:
                    ctx.Flip(SixLabors.ImageSharp.Processing.FlipMode.Vertical);
                    break;
                case FlipMode.Both:
                    ctx.Flip(SixLabors.ImageSharp.Processing.FlipMode.Horizontal);
                    ctx.Flip(SixLabors.ImageSharp.Processing.FlipMode.Vertical);
                    break;
            }
        });
        return SaveToBytes(image);
    }

    #endregion

    #region Color Operations

    /// <summary>
    /// Converts an image to grayscale.
    /// </summary>
    public byte[] ToGrayscale(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Grayscale());
        return SaveToBytes(image);
    }

    /// <summary>
    /// Adjusts image brightness.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="brightness">Brightness factor (1.0 = no change, >1 = brighter, &lt;1 = darker).</param>
    public byte[] AdjustBrightness(byte[] imageData, float brightness)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Brightness(brightness));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Adjusts image contrast.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="contrast">Contrast factor (1.0 = no change, >1 = more contrast, &lt;1 = less contrast).</param>
    public byte[] AdjustContrast(byte[] imageData, float contrast)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Contrast(contrast));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Inverts image colors.
    /// </summary>
    public byte[] Invert(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Invert());
        return SaveToBytes(image);
    }

    /// <summary>
    /// Adjusts image saturation.
    /// </summary>
    public byte[] AdjustSaturation(byte[] imageData, float saturation)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.Saturate(saturation));
        return SaveToBytes(image);
    }

    #endregion

    #region Enhancement

    /// <summary>
    /// Sharpens the image.
    /// </summary>
    public byte[] Sharpen(byte[] imageData, float sigma = 3f)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.GaussianSharpen(sigma));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Applies Gaussian blur to denoise the image.
    /// </summary>
    public byte[] Denoise(byte[] imageData, float sigma = 1f)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.GaussianBlur(sigma));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Enhances image for OCR by applying multiple transformations.
    /// </summary>
    public byte[] EnhanceForOcr(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        // Upscale for better recognition
        int newWidth = image.Width * 2;
        int newHeight = image.Height * 2;
        
        image.Mutate(ctx =>
        {
            ctx.Resize(newWidth, newHeight, KnownResamplers.Lanczos3);
            ctx.Grayscale();
            ctx.Contrast(1.2f);
            ctx.GaussianSharpen(1f);
        });
        
        return SaveToBytes(image);
    }

    /// <summary>
    /// Applies binary thresholding to the image.
    /// </summary>
    public byte[] Binarize(byte[] imageData, float threshold = 0.5f)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        image.Mutate(ctx => ctx.BinaryThreshold(threshold));
        return SaveToBytes(image);
    }

    /// <summary>
    /// Applies adaptive thresholding for documents with varying lighting.
    /// </summary>
    public byte[] AdaptiveThreshold(byte[] imageData, int blockSize = 11)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        // Convert to grayscale first
        image.Mutate(ctx => ctx.Grayscale());
        
        // Apply local binary threshold approximation
        // ImageSharp doesn't have true adaptive threshold, so we use a workaround
        image.Mutate(ctx =>
        {
            ctx.Contrast(1.3f);
            ctx.BinaryThreshold(0.5f);
        });
        
        return SaveToBytes(image);
    }

    #endregion

    #region Analysis

    /// <summary>
    /// Gets the dominant color from an image.
    /// </summary>
    public System.Drawing.Color GetDominantColor(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        long totalR = 0, totalG = 0, totalB = 0;
        int pixelCount = 0;
        
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    totalR += row[x].R;
                    totalG += row[x].G;
                    totalB += row[x].B;
                    pixelCount++;
                }
            }
        });
        
        if (pixelCount == 0)
            return System.Drawing.Color.Black;
        
        return System.Drawing.Color.FromArgb(
            (int)(totalR / pixelCount),
            (int)(totalG / pixelCount),
            (int)(totalB / pixelCount));
    }

    /// <summary>
    /// Gets the average color from a region of the image.
    /// </summary>
    public System.Drawing.Color GetAverageColor(byte[] imageData, System.Drawing.Rectangle? region = null)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        int startX = region?.X ?? 0;
        int startY = region?.Y ?? 0;
        int endX = region.HasValue ? region.Value.Right : image.Width;
        int endY = region.HasValue ? region.Value.Bottom : image.Height;
        
        long totalR = 0, totalG = 0, totalB = 0;
        int pixelCount = 0;
        
        image.ProcessPixelRows(accessor =>
        {
            for (int y = startY; y < Math.Min(endY, accessor.Height); y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = startX; x < Math.Min(endX, row.Length); x++)
                {
                    totalR += row[x].R;
                    totalG += row[x].G;
                    totalB += row[x].B;
                    pixelCount++;
                }
            }
        });
        
        if (pixelCount == 0)
            return System.Drawing.Color.Black;
        
        return System.Drawing.Color.FromArgb(
            (int)(totalR / pixelCount),
            (int)(totalG / pixelCount),
            (int)(totalB / pixelCount));
    }

    /// <summary>
    /// Gets the average brightness of an image (0.0 to 1.0).
    /// </summary>
    public double GetBrightness(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        double totalBrightness = 0;
        int pixelCount = 0;
        
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    // Calculate perceived brightness
                    double brightness = (0.299 * row[x].R + 0.587 * row[x].G + 0.114 * row[x].B) / 255.0;
                    totalBrightness += brightness;
                    pixelCount++;
                }
            }
        });
        
        return pixelCount > 0 ? totalBrightness / pixelCount : 0;
    }

    /// <summary>
    /// Gets the contrast level of an image.
    /// </summary>
    public double GetContrast(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        var values = new List<double>();
        
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    double brightness = (0.299 * row[x].R + 0.587 * row[x].G + 0.114 * row[x].B) / 255.0;
                    values.Add(brightness);
                }
            }
        });
        
        if (values.Count == 0)
            return 0;
        
        // Calculate standard deviation as contrast measure
        double mean = values.Average();
        double sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquares / values.Count);
    }

    /// <summary>
    /// Gets the dimensions of an image.
    /// </summary>
    public (int Width, int Height) GetDimensions(byte[] imageData)
    {
        var info = SharpImage.Identify(imageData);
        return (info.Width, info.Height);
    }

    #endregion

    #region Helpers

    private static byte[] SaveToBytes(Image<Rgba32> image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    #endregion
}

/// <summary>
/// Resize mode options.
/// </summary>
public enum ResizeMode
{
    /// <summary>Stretch to fill, may distort aspect ratio.</summary>
    Stretch,
    /// <summary>Fit within bounds, preserving aspect ratio.</summary>
    Fit,
    /// <summary>Fill bounds, preserving aspect ratio, may crop.</summary>
    Fill,
    /// <summary>Pad to fill bounds, preserving aspect ratio.</summary>
    Pad
}

/// <summary>
/// Flip mode options.
/// </summary>
public enum FlipMode
{
    /// <summary>Flip horizontally (mirror).</summary>
    Horizontal,
    /// <summary>Flip vertically.</summary>
    Vertical,
    /// <summary>Flip both horizontally and vertically.</summary>
    Both
}

