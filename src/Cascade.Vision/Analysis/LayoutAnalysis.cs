using System.Drawing;

namespace Cascade.Vision.Analysis;

/// <summary>
/// Represents the result of a UI layout analysis.
/// </summary>
public class LayoutAnalysis
{
    /// <summary>
    /// Gets or sets the detected layout regions.
    /// </summary>
    public IReadOnlyList<LayoutRegion> Regions { get; set; } = Array.Empty<LayoutRegion>();

    /// <summary>
    /// Gets or sets the detected layout type.
    /// </summary>
    public LayoutType DetectedLayout { get; set; }

    /// <summary>
    /// Gets or sets the main content area.
    /// </summary>
    public Rectangle ContentArea { get; set; }

    /// <summary>
    /// Gets or sets the header area if detected.
    /// </summary>
    public Rectangle? HeaderArea { get; set; }

    /// <summary>
    /// Gets or sets the footer area if detected.
    /// </summary>
    public Rectangle? FooterArea { get; set; }

    /// <summary>
    /// Gets or sets the sidebar area if detected.
    /// </summary>
    public Rectangle? SidebarArea { get; set; }

    /// <summary>
    /// Gets or sets the navigation area if detected.
    /// </summary>
    public Rectangle? NavigationArea { get; set; }

    /// <summary>
    /// Gets or sets the total image dimensions.
    /// </summary>
    public Size ImageSize { get; set; }

    /// <summary>
    /// Gets or sets the analysis confidence (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }
}

/// <summary>
/// Represents a detected region in the UI layout.
/// </summary>
public class LayoutRegion
{
    /// <summary>
    /// Gets or sets the region name/identifier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the region bounds.
    /// </summary>
    public Rectangle Bounds { get; set; }

    /// <summary>
    /// Gets or sets the region type.
    /// </summary>
    public LayoutRegionType Type { get; set; }

    /// <summary>
    /// Gets or sets the elements within this region.
    /// </summary>
    public IReadOnlyList<VisualElement> Elements { get; set; } = Array.Empty<VisualElement>();

    /// <summary>
    /// Gets or sets the dominant color of this region.
    /// </summary>
    public Color? DominantColor { get; set; }

    /// <summary>
    /// Gets the center point of the region.
    /// </summary>
    public Point Center => new(
        Bounds.X + Bounds.Width / 2,
        Bounds.Y + Bounds.Height / 2);
}

/// <summary>
/// Types of layout regions.
/// </summary>
public enum LayoutRegionType
{
    /// <summary>Unknown region type.</summary>
    Unknown,
    
    /// <summary>Header/title bar area.</summary>
    Header,
    
    /// <summary>Footer/status area.</summary>
    Footer,
    
    /// <summary>Main content area.</summary>
    Content,
    
    /// <summary>Sidebar/navigation area.</summary>
    Sidebar,
    
    /// <summary>Navigation/menu area.</summary>
    Navigation,
    
    /// <summary>Toolbar area.</summary>
    Toolbar,
    
    /// <summary>Form/input area.</summary>
    Form,
    
    /// <summary>List/table area.</summary>
    DataView,
    
    /// <summary>Image/media area.</summary>
    Media,
    
    /// <summary>Advertisement area.</summary>
    Advertisement
}

/// <summary>
/// Types of detected layout patterns.
/// </summary>
public enum LayoutType
{
    /// <summary>Unknown layout type.</summary>
    Unknown,
    
    /// <summary>Single column layout.</summary>
    SingleColumn,
    
    /// <summary>Two column layout.</summary>
    TwoColumn,
    
    /// <summary>Three column layout.</summary>
    ThreeColumn,
    
    /// <summary>Dashboard-style layout with widgets.</summary>
    Dashboard,
    
    /// <summary>Form layout with labels and inputs.</summary>
    Form,
    
    /// <summary>List/table layout.</summary>
    List,
    
    /// <summary>Grid layout.</summary>
    Grid,
    
    /// <summary>Dialog/modal layout.</summary>
    Dialog,
    
    /// <summary>Wizard/step layout.</summary>
    Wizard,
    
    /// <summary>Master-detail layout.</summary>
    MasterDetail
}

