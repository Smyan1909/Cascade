using System.Drawing;

namespace Cascade.Vision.Analysis;

public class VisualElement
{
    public VisualElementType Type { get; set; } = VisualElementType.Unknown;
    public Rectangle BoundingBox { get; set; }
    public double Confidence { get; set; }
    public string? Text { get; set; }
    public Color? DominantColor { get; set; }
    public Color? BackgroundColor { get; set; }
    public bool IsClickable { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum VisualElementType
{
    Unknown,
    Button,
    TextBox,
    Label,
    Image,
    Icon,
    Checkbox,
    RadioButton,
    Dropdown,
    Menu,
    MenuBar,
    Toolbar,
    StatusBar,
    Tab,
    Link,
    Table,
    List,
    Tree,
    Dialog,
    Panel
}


