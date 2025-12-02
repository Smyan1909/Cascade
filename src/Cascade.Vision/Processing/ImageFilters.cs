using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SharpImage = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Processing;

/// <summary>
/// Collection of image filter operations for OCR preprocessing.
/// </summary>
public static class ImageFilters
{
    /// <summary>
    /// Applies Otsu's binarization method to find optimal threshold.
    /// </summary>
    public static byte[] OtsuBinarize(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        // Calculate histogram
        var histogram = new int[256];
        int total = image.Width * image.Height;
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                int gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                histogram[gray]++;
            }
        }
        
        // Find optimal threshold using Otsu's method
        float sum = 0;
        for (int i = 0; i < 256; i++)
            sum += i * histogram[i];
        
        float sumB = 0;
        int wB = 0;
        float maxVariance = 0;
        int threshold = 0;
        
        for (int t = 0; t < 256; t++)
        {
            wB += histogram[t];
            if (wB == 0) continue;
            
            int wF = total - wB;
            if (wF == 0) break;
            
            sumB += t * histogram[t];
            float mB = sumB / wB;
            float mF = (sum - sumB) / wF;
            
            float variance = wB * wF * (mB - mF) * (mB - mF);
            
            if (variance > maxVariance)
            {
                maxVariance = variance;
                threshold = t;
            }
        }
        
        // Apply threshold
        image.Mutate(ctx => ctx.BinaryThreshold(threshold / 255f));
        
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// Applies morphological erosion to remove small noise.
    /// </summary>
    public static byte[] Erode(byte[] imageData, int kernelSize = 3)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        var result = new Image<Rgba32>(image.Width, image.Height);
        int halfKernel = kernelSize / 2;

        for (int y = halfKernel; y < image.Height - halfKernel; y++)
        {
            for (int x = halfKernel; x < image.Width - halfKernel; x++)
            {
                byte minVal = 255;
                
                for (int ky = -halfKernel; ky <= halfKernel; ky++)
                {
                    for (int kx = -halfKernel; kx <= halfKernel; kx++)
                    {
                        var pixel = image[x + kx, y + ky];
                        byte val = (byte)((pixel.R + pixel.G + pixel.B) / 3);
                        if (val < minVal) minVal = val;
                    }
                }
                
                result[x, y] = new Rgba32(minVal, minVal, minVal, 255);
            }
        }

        using var ms = new MemoryStream();
        result.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// Applies morphological dilation to fill small gaps.
    /// </summary>
    public static byte[] Dilate(byte[] imageData, int kernelSize = 3)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        var result = new Image<Rgba32>(image.Width, image.Height);
        int halfKernel = kernelSize / 2;

        for (int y = halfKernel; y < image.Height - halfKernel; y++)
        {
            for (int x = halfKernel; x < image.Width - halfKernel; x++)
            {
                byte maxVal = 0;
                
                for (int ky = -halfKernel; ky <= halfKernel; ky++)
                {
                    for (int kx = -halfKernel; kx <= halfKernel; kx++)
                    {
                        var pixel = image[x + kx, y + ky];
                        byte val = (byte)((pixel.R + pixel.G + pixel.B) / 3);
                        if (val > maxVal) maxVal = val;
                    }
                }
                
                result[x, y] = new Rgba32(maxVal, maxVal, maxVal, 255);
            }
        }

        using var ms = new MemoryStream();
        result.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// Applies morphological opening (erosion followed by dilation) to remove noise.
    /// </summary>
    public static byte[] Opening(byte[] imageData, int kernelSize = 3)
    {
        var eroded = Erode(imageData, kernelSize);
        return Dilate(eroded, kernelSize);
    }

    /// <summary>
    /// Applies morphological closing (dilation followed by erosion) to fill gaps.
    /// </summary>
    public static byte[] Closing(byte[] imageData, int kernelSize = 3)
    {
        var dilated = Dilate(imageData, kernelSize);
        return Erode(dilated, kernelSize);
    }

    /// <summary>
    /// Applies edge detection using Sobel operator.
    /// </summary>
    public static byte[] SobelEdgeDetection(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        // Apply Sobel edge detection using ImageSharp's built-in operator
        image.Mutate(ctx => ctx.DetectEdges());
        
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// Applies histogram equalization to improve contrast.
    /// </summary>
    public static byte[] HistogramEqualization(byte[] imageData)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        
        // Calculate histogram
        var histogram = new int[256];
        int pixelCount = image.Width * image.Height;
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                int gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                histogram[gray]++;
            }
        }
        
        // Calculate cumulative distribution function
        var cdf = new int[256];
        cdf[0] = histogram[0];
        for (int i = 1; i < 256; i++)
            cdf[i] = cdf[i - 1] + histogram[i];
        
        // Find minimum non-zero CDF value
        int cdfMin = 0;
        for (int i = 0; i < 256; i++)
        {
            if (cdf[i] > 0)
            {
                cdfMin = cdf[i];
                break;
            }
        }
        
        // Create lookup table
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            lut[i] = (byte)Math.Round((double)(cdf[i] - cdfMin) / (pixelCount - cdfMin) * 255);
        }
        
        // Apply equalization
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                int gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                byte newVal = lut[gray];
                image[x, y] = new Rgba32(newVal, newVal, newVal, pixel.A);
            }
        }
        
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// Removes speckle noise using median filter.
    /// </summary>
    public static byte[] MedianFilter(byte[] imageData, int kernelSize = 3)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);
        var result = new Image<Rgba32>(image.Width, image.Height);
        int halfKernel = kernelSize / 2;

        for (int y = halfKernel; y < image.Height - halfKernel; y++)
        {
            for (int x = halfKernel; x < image.Width - halfKernel; x++)
            {
                var values = new List<byte>();
                
                for (int ky = -halfKernel; ky <= halfKernel; ky++)
                {
                    for (int kx = -halfKernel; kx <= halfKernel; kx++)
                    {
                        var pixel = image[x + kx, y + ky];
                        byte val = (byte)((pixel.R + pixel.G + pixel.B) / 3);
                        values.Add(val);
                    }
                }
                
                values.Sort();
                byte median = values[values.Count / 2];
                result[x, y] = new Rgba32(median, median, median, 255);
            }
        }

        using var ms = new MemoryStream();
        result.Save(ms, new PngEncoder());
        return ms.ToArray();
    }
}
