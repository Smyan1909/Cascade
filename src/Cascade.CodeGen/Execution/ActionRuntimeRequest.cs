using Cascade.CodeGen.Generation;

namespace Cascade.CodeGen.Execution;

public sealed class ActionRuntimeRequest
{
    public ActionRuntimeRequest(string name, ActionType type)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Action name is required.", nameof(name)) : name;
        Type = type;
    }

    public string Name { get; }
    public string Description { get; set; } = string.Empty;
    public ActionType Type { get; }
    public TimeSpan? Delay { get; set; }
    public int RetryCount { get; set; } = 3;
    public bool CaptureScreenshotBefore { get; set; }
    public bool CaptureScreenshotAfter { get; set; }
    public Dictionary<string, object> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);
}

