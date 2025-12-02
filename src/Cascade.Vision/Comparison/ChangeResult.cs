using System.Drawing;

namespace Cascade.Vision.Comparison;

/// <summary>
/// Represents the result of an image comparison operation.
/// </summary>
public class ChangeResult
{
    /// <summary>
    /// Gets or sets whether any changes were detected.
    /// </summary>
    public bool HasChanges { get; set; }

    /// <summary>
    /// Gets or sets the percentage of pixels that changed (0.0 to 1.0).
    /// </summary>
    public double DifferencePercentage { get; set; }

    /// <summary>
    /// Gets or sets the number of pixels that changed.
    /// </summary>
    public int ChangedPixelCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of pixels compared.
    /// </summary>
    public int TotalPixelCount { get; set; }

    /// <summary>
    /// Gets or sets the bounding rectangles of changed regions.
    /// </summary>
    public IReadOnlyList<Rectangle> ChangedRegions { get; set; } = Array.Empty<Rectangle>();

    /// <summary>
    /// Gets or sets the difference image (visual diff) if generated.
    /// </summary>
    public byte[]? DifferenceImage { get; set; }

    /// <summary>
    /// Gets or sets the type/magnitude of change detected.
    /// </summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>
    /// Gets or sets whether new visual elements appeared.
    /// </summary>
    public bool HasNewElements { get; set; }

    /// <summary>
    /// Gets or sets whether visual elements were removed.
    /// </summary>
    public bool HasRemovedElements { get; set; }

    /// <summary>
    /// Gets or sets whether text content changed.
    /// </summary>
    public bool HasTextChanges { get; set; }

    /// <summary>
    /// Gets or sets the time taken for the comparison.
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Creates a result indicating no changes.
    /// </summary>
    public static ChangeResult NoChange => new()
    {
        HasChanges = false,
        DifferencePercentage = 0,
        ChangedPixelCount = 0,
        ChangeType = ChangeType.None,
        ChangedRegions = Array.Empty<Rectangle>()
    };
}

/// <summary>
/// Categorizes the magnitude of detected changes.
/// </summary>
public enum ChangeType
{
    /// <summary>No changes detected.</summary>
    None,

    /// <summary>Minor changes (less than 5% of pixels).</summary>
    Minor,

    /// <summary>Moderate changes (5-20% of pixels).</summary>
    Moderate,

    /// <summary>Major changes (more than 20% of pixels).</summary>
    Major,

    /// <summary>Complete change (entirely different content).</summary>
    Complete
}

