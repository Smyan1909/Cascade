using Cascade.UIAutomation.Discovery;

namespace Cascade.CodeGen.Generation;

/// <summary>
/// Defines a UI automation action to be performed.
/// </summary>
public class ActionDefinition
{
    /// <summary>
    /// Name of the action (used for method naming).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the action does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type of action to perform.
    /// </summary>
    public ActionType Type { get; set; }

    /// <summary>
    /// Locator for the target element.
    /// </summary>
    public ElementLocator TargetElement { get; set; } = null!;

    /// <summary>
    /// Additional parameters for the action (action-specific).
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Optional delay to wait after the action completes.
    /// </summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>
    /// Number of times to retry the action if it fails.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Whether to capture a screenshot before the action.
    /// </summary>
    public bool CaptureScreenshotBefore { get; set; } = false;

    /// <summary>
    /// Whether to capture a screenshot after the action.
    /// </summary>
    public bool CaptureScreenshotAfter { get; set; } = false;
}

