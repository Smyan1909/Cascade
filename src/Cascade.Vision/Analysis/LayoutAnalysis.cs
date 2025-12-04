namespace Cascade.Vision.Analysis;

public class LayoutAnalysis
{
    public IReadOnlyList<LayoutRegion> Regions { get; init; } = Array.Empty<LayoutRegion>();
    public LayoutType DetectedLayout { get; init; } = LayoutType.Unknown;
    public Rectangle ContentArea { get; init; }
    public Rectangle? HeaderArea { get; init; }
    public Rectangle? FooterArea { get; init; }
    public Rectangle? SidebarArea { get; init; }
    public Rectangle? NavigationArea { get; init; }
}

public class LayoutRegion
{
    public string Name { get; init; } = string.Empty;
    public Rectangle Bounds { get; init; }
    public LayoutRegionType Type { get; init; } = LayoutRegionType.Unknown;
    public IReadOnlyList<VisualElement> Elements { get; init; } = Array.Empty<VisualElement>();
}

public enum LayoutRegionType
{
    Unknown,
    Header,
    Footer,
    Sidebar,
    Content,
    Navigation
}

public enum LayoutType
{
    Unknown,
    SingleColumn,
    TwoColumn,
    ThreeColumn,
    Dashboard,
    Form,
    List,
    Grid,
    Dialog,
    Wizard
}


