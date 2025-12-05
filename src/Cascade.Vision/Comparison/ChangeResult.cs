using System.Drawing;

namespace Cascade.Vision.Comparison;

public class ChangeResult
{
    public bool HasChanges { get; set; }
    public double DifferencePercentage { get; set; }
    public IReadOnlyList<Rectangle> ChangedRegions { get; set; } = Array.Empty<Rectangle>();
    public byte[]? DifferenceImage { get; set; }
    public ChangeType ChangeType { get; set; } = ChangeType.None;
    public bool HasNewElements { get; set; }
    public bool HasRemovedElements { get; set; }
    public bool HasTextChanges { get; set; }
}

public enum ChangeType
{
    None,
    Minor,
    Moderate,
    Major,
    Complete
}


