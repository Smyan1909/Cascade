using System.Drawing;

namespace Cascade.CodeGen.Recording;

/// <summary>
/// Options for action recording.
/// </summary>
public class RecordingOptions
{
    public bool RecordMouseClicks { get; set; } = true;
    public bool RecordKeystrokes { get; set; } = true;
    public bool RecordScrolls { get; set; } = true;
    public bool CaptureScreenshots { get; set; } = true;
    public TimeSpan MinActionInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public IReadOnlyList<string> ExcludedProcesses { get; set; } = new List<string>();
    public Rectangle? CaptureRegion { get; set; }
}

