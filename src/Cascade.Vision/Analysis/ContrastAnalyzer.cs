using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SharpImage = SixLabors.ImageSharp.Image;

namespace Cascade.Vision.Analysis;

/// <summary>
/// Analyzes text/background contrast for accessibility and OCR optimization.
/// </summary>
public class ContrastAnalyzer
{
    /// <summary>
    /// Calculates the contrast ratio between two colors.
    /// Based on WCAG 2.0 contrast ratio formula.
    /// </summary>
    /// <param name="foreground">The foreground (text) color.</param>
    /// <param name="background">The background color.</param>
    /// <returns>Contrast ratio (1:1 to 21:1).</returns>
    public double CalculateContrastRatio(System.Drawing.Color foreground, System.Drawing.Color background)
    {
        double fgLuminance = GetRelativeLuminance(foreground);
        double bgLuminance = GetRelativeLuminance(background);

        double lighter = Math.Max(fgLuminance, bgLuminance);
        double darker = Math.Min(fgLuminance, bgLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Checks if the contrast ratio meets WCAG AA standard for normal text (4.5:1).
    /// </summary>
    public bool MeetsWcagAA(System.Drawing.Color foreground, System.Drawing.Color background)
    {
        return CalculateContrastRatio(foreground, background) >= 4.5;
    }

    /// <summary>
    /// Checks if the contrast ratio meets WCAG AAA standard for normal text (7:1).
    /// </summary>
    public bool MeetsWcagAAA(System.Drawing.Color foreground, System.Drawing.Color background)
    {
        return CalculateContrastRatio(foreground, background) >= 7.0;
    }

    /// <summary>
    /// Analyzes the contrast in a region of an image.
    /// </summary>
    /// <param name="imageData">The image data.</param>
    /// <param name="region">Optional region to analyze.</param>
    /// <returns>Contrast analysis result.</returns>
    public ContrastAnalysisResult AnalyzeContrast(byte[] imageData, System.Drawing.Rectangle? region = null)
    {
        using var image = SharpImage.Load<Rgba32>(imageData);

        int startX = region?.X ?? 0;
        int startY = region?.Y ?? 0;
        int endX = region?.Right ?? image.Width;
        int endY = region?.Bottom ?? image.Height;

        // Collect all colors
        var colors = new List<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (int y = startY; y < Math.Min(endY, accessor.Height); y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = startX; x < Math.Min(endX, row.Length); x++)
                {
                    colors.Add(row[x]);
                }
            }
        });

        if (colors.Count == 0)
        {
            return new ContrastAnalysisResult
            {
                IsAnalyzable = false
            };
        }

        // Find dominant colors (likely background and text)
        var (background, foreground) = FindDominantColors(colors);

        var bgColor = System.Drawing.Color.FromArgb(background.R, background.G, background.B);
        var fgColor = System.Drawing.Color.FromArgb(foreground.R, foreground.G, foreground.B);

        double contrastRatio = CalculateContrastRatio(fgColor, bgColor);

        return new ContrastAnalysisResult
        {
            IsAnalyzable = true,
            BackgroundColor = bgColor,
            ForegroundColor = fgColor,
            ContrastRatio = contrastRatio,
            MeetsWcagAA = contrastRatio >= 4.5,
            MeetsWcagAAA = contrastRatio >= 7.0,
            IsSuitableForOcr = contrastRatio >= 3.0,
            Recommendation = GetRecommendation(contrastRatio)
        };
    }

    /// <summary>
    /// Gets a color that provides good contrast against the given background.
    /// </summary>
    public System.Drawing.Color GetContrastingColor(System.Drawing.Color background)
    {
        double luminance = GetRelativeLuminance(background);
        return luminance > 0.179 ? System.Drawing.Color.Black : System.Drawing.Color.White;
    }

    /// <summary>
    /// Suggests improved colors to meet target contrast ratio.
    /// </summary>
    public (System.Drawing.Color foreground, System.Drawing.Color background) SuggestImprovedColors(
        System.Drawing.Color foreground, 
        System.Drawing.Color background, 
        double targetRatio = 4.5)
    {
        double currentRatio = CalculateContrastRatio(foreground, background);
        
        if (currentRatio >= targetRatio)
            return (foreground, background);

        // Try darkening foreground
        var darkerFg = DarkenColor(foreground);
        if (CalculateContrastRatio(darkerFg, background) >= targetRatio)
            return (darkerFg, background);

        // Try lightening background
        var lighterBg = LightenColor(background);
        if (CalculateContrastRatio(foreground, lighterBg) >= targetRatio)
            return (foreground, lighterBg);

        // Use black/white as fallback
        var fgLuminance = GetRelativeLuminance(foreground);
        var bgLuminance = GetRelativeLuminance(background);

        if (bgLuminance > 0.5)
            return (System.Drawing.Color.Black, background);
        else
            return (System.Drawing.Color.White, background);
    }

    private static double GetRelativeLuminance(System.Drawing.Color color)
    {
        double r = GetLuminanceComponent(color.R / 255.0);
        double g = GetLuminanceComponent(color.G / 255.0);
        double b = GetLuminanceComponent(color.B / 255.0);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double GetLuminanceComponent(double value)
    {
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static (Rgba32 background, Rgba32 foreground) FindDominantColors(List<Rgba32> colors)
    {
        // Simple k-means-like clustering for 2 colors
        var sorted = colors
            .GroupBy(c => GetBrightnessGroup(c))
            .OrderByDescending(g => g.Count())
            .Take(2)
            .ToList();

        if (sorted.Count < 2)
        {
            var avg = AverageColor(colors);
            return (avg, InvertColor(avg));
        }

        var color1 = AverageColor(sorted[0].ToList());
        var color2 = AverageColor(sorted[1].ToList());

        // The brighter one is likely background
        if (GetBrightness(color1) > GetBrightness(color2))
            return (color1, color2);
        else
            return (color2, color1);
    }

    private static int GetBrightnessGroup(Rgba32 color)
    {
        int brightness = (color.R + color.G + color.B) / 3;
        return brightness / 32; // 8 groups
    }

    private static int GetBrightness(Rgba32 color)
    {
        return (color.R + color.G + color.B) / 3;
    }

    private static Rgba32 AverageColor(List<Rgba32> colors)
    {
        if (colors.Count == 0)
            return new Rgba32(128, 128, 128, 255);

        long r = 0, g = 0, b = 0;
        foreach (var c in colors)
        {
            r += c.R;
            g += c.G;
            b += c.B;
        }

        return new Rgba32(
            (byte)(r / colors.Count),
            (byte)(g / colors.Count),
            (byte)(b / colors.Count),
            255);
    }

    private static Rgba32 InvertColor(Rgba32 color)
    {
        return new Rgba32(
            (byte)(255 - color.R),
            (byte)(255 - color.G),
            (byte)(255 - color.B),
            255);
    }

    private static System.Drawing.Color DarkenColor(System.Drawing.Color color)
    {
        return System.Drawing.Color.FromArgb(
            Math.Max(0, color.R - 50),
            Math.Max(0, color.G - 50),
            Math.Max(0, color.B - 50));
    }

    private static System.Drawing.Color LightenColor(System.Drawing.Color color)
    {
        return System.Drawing.Color.FromArgb(
            Math.Min(255, color.R + 50),
            Math.Min(255, color.G + 50),
            Math.Min(255, color.B + 50));
    }

    private static string GetRecommendation(double contrastRatio)
    {
        if (contrastRatio >= 7.0)
            return "Excellent contrast - meets WCAG AAA standard";
        if (contrastRatio >= 4.5)
            return "Good contrast - meets WCAG AA standard";
        if (contrastRatio >= 3.0)
            return "Acceptable for large text, may need improvement for normal text";
        return "Poor contrast - not suitable for text, preprocessing recommended for OCR";
    }
}

/// <summary>
/// Result of a contrast analysis operation.
/// </summary>
public class ContrastAnalysisResult
{
    /// <summary>
    /// Gets or sets whether the region was analyzable.
    /// </summary>
    public bool IsAnalyzable { get; set; }

    /// <summary>
    /// Gets or sets the detected background color.
    /// </summary>
    public System.Drawing.Color BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the detected foreground/text color.
    /// </summary>
    public System.Drawing.Color ForegroundColor { get; set; }

    /// <summary>
    /// Gets or sets the calculated contrast ratio.
    /// </summary>
    public double ContrastRatio { get; set; }

    /// <summary>
    /// Gets or sets whether WCAG AA standard is met.
    /// </summary>
    public bool MeetsWcagAA { get; set; }

    /// <summary>
    /// Gets or sets whether WCAG AAA standard is met.
    /// </summary>
    public bool MeetsWcagAAA { get; set; }

    /// <summary>
    /// Gets or sets whether the contrast is suitable for OCR.
    /// </summary>
    public bool IsSuitableForOcr { get; set; }

    /// <summary>
    /// Gets or sets the recommendation for improving contrast.
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
}

