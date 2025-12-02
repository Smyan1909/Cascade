using System.Drawing;
using Cascade.CodeGen.Generation;
using Cascade.UIAutomation.Elements;

namespace Cascade.CodeGen.Recording;

/// <summary>
/// Represents a single recorded UI action.
/// </summary>
public class RecordedAction
{
    public int Index { get; set; }
    public DateTime Timestamp { get; set; }
    public ActionType Type { get; set; }
    public ElementSnapshot? TargetElement { get; set; }
    public Point? MousePosition { get; set; }
    public string? TypedText { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public byte[]? Screenshot { get; set; }
    public TimeSpan? DurationSincePrevious { get; set; }
}

