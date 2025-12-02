using System.Drawing;

namespace Cascade.Vision.Analysis;

/// <summary>
/// Represents a visually detected UI element.
/// </summary>
public class VisualElement
{
    /// <summary>
    /// Gets or sets the detected element type.
    /// </summary>
    public VisualElementType Type { get; set; }

    /// <summary>
    /// Gets or sets the bounding box of the element.
    /// </summary>
    public Rectangle BoundingBox { get; set; }

    /// <summary>
    /// Gets or sets the detection confidence (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the detected text within the element.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the dominant color of the element.
    /// </summary>
    public Color? DominantColor { get; set; }

    /// <summary>
    /// Gets or sets the background color of the element.
    /// </summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets whether the element appears clickable.
    /// </summary>
    public bool IsClickable { get; set; }

    /// <summary>
    /// Gets or sets additional properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Gets the center point of the element.
    /// </summary>
    public Point Center => new(
        BoundingBox.X + BoundingBox.Width / 2,
        BoundingBox.Y + BoundingBox.Height / 2);

    /// <summary>
    /// Gets the area of the element in pixels.
    /// </summary>
    public int Area => BoundingBox.Width * BoundingBox.Height;
}

/// <summary>
/// Types of visual elements that can be detected.
/// </summary>
public enum VisualElementType
{
    /// <summary>Unknown element type.</summary>
    Unknown,
    
    /// <summary>Button control.</summary>
    Button,
    
    /// <summary>Text input box.</summary>
    TextBox,
    
    /// <summary>Static text label.</summary>
    Label,
    
    /// <summary>Image or picture.</summary>
    Image,
    
    /// <summary>Icon or small graphic.</summary>
    Icon,
    
    /// <summary>Checkbox control.</summary>
    Checkbox,
    
    /// <summary>Radio button control.</summary>
    RadioButton,
    
    /// <summary>Dropdown/combo box.</summary>
    Dropdown,
    
    /// <summary>Menu item.</summary>
    Menu,
    
    /// <summary>Menu bar.</summary>
    MenuBar,
    
    /// <summary>Toolbar.</summary>
    Toolbar,
    
    /// <summary>Status bar.</summary>
    StatusBar,
    
    /// <summary>Tab control.</summary>
    Tab,
    
    /// <summary>Hyperlink.</summary>
    Link,
    
    /// <summary>Table or grid.</summary>
    Table,
    
    /// <summary>List control.</summary>
    List,
    
    /// <summary>Tree view.</summary>
    Tree,
    
    /// <summary>Dialog/modal window.</summary>
    Dialog,
    
    /// <summary>Panel or container.</summary>
    Panel,
    
    /// <summary>Scrollbar.</summary>
    Scrollbar,
    
    /// <summary>Slider control.</summary>
    Slider,
    
    /// <summary>Progress bar.</summary>
    ProgressBar
}

