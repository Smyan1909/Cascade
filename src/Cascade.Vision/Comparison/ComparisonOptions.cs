namespace Cascade.Vision.Comparison;

public class ComparisonOptions
{
    public double ChangeThreshold { get; set; } = 0.01;
    public bool IgnoreAntiAliasing { get; set; } = true;
    public bool IgnoreMinorColorDifferences { get; set; } = true;
    public int ColorTolerance { get; set; } = 10;
    public IReadOnlyList<Rectangle>? IgnoreRegions { get; set; }
    public bool GenerateDifferenceImage { get; set; } = true;
    public Color DifferenceHighlightColor { get; set; } = Color.Red;
}


