using System.Drawing;

namespace Cascade.Vision.Comparison;

/// <summary>
/// Configuration options for image comparison operations.
/// </summary>
public class ComparisonOptions
{
    /// <summary>
    /// Gets or sets the threshold for considering a change significant (0.0 to 1.0).
    /// Default is 0.01 (1% of pixels changed).
    /// </summary>
    public double ChangeThreshold { get; set; } = 0.01;

    /// <summary>
    /// Gets or sets whether to ignore anti-aliasing artifacts.
    /// </summary>
    public bool IgnoreAntiAliasing { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to ignore minor color differences.
    /// </summary>
    public bool IgnoreMinorColorDifferences { get; set; } = true;

    /// <summary>
    /// Gets or sets the RGB color tolerance (0-255) for considering pixels equal.
    /// </summary>
    public int ColorTolerance { get; set; } = 10;

    /// <summary>
    /// Gets or sets regions to ignore during comparison.
    /// </summary>
    public IReadOnlyList<Rectangle>? IgnoreRegions { get; set; }

    /// <summary>
    /// Gets or sets whether to generate a difference image.
    /// </summary>
    public bool GenerateDifferenceImage { get; set; } = true;

    /// <summary>
    /// Gets or sets the color to use for highlighting differences.
    /// </summary>
    public Color DifferenceHighlightColor { get; set; } = Color.Red;

    /// <summary>
    /// Gets or sets the alpha value for the difference overlay (0-255).
    /// </summary>
    public byte DifferenceOverlayAlpha { get; set; } = 128;

    /// <summary>
    /// Gets or sets the minimum size of a region to report as changed.
    /// </summary>
    public int MinChangedRegionSize { get; set; } = 10;

    /// <summary>
    /// Creates default comparison options.
    /// </summary>
    public static ComparisonOptions Default => new();

    /// <summary>
    /// Creates strict comparison options that detect small changes.
    /// </summary>
    public static ComparisonOptions Strict => new()
    {
        ChangeThreshold = 0.001,
        ColorTolerance = 0,
        IgnoreAntiAliasing = false,
        IgnoreMinorColorDifferences = false
    };

    /// <summary>
    /// Creates lenient comparison options that ignore minor variations.
    /// </summary>
    public static ComparisonOptions Lenient => new()
    {
        ChangeThreshold = 0.05,
        ColorTolerance = 30,
        IgnoreAntiAliasing = true,
        IgnoreMinorColorDifferences = true
    };
}

