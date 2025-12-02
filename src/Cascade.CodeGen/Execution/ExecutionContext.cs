using System.Reflection;
using Cascade.UIAutomation.Discovery;
using Cascade.Vision.Capture;
using Cascade.Vision.OCR;

namespace Cascade.CodeGen.Execution;

/// <summary>
/// Runtime context for script execution.
/// </summary>
public class ExecutionContext
{
    /// <summary>
    /// Unique identifier for this execution.
    /// </summary>
    public Guid ExecutionId { get; } = Guid.NewGuid();

    /// <summary>
    /// Service provider for dependency injection.
    /// </summary>
    public IServiceProvider? Services { get; set; }

    /// <summary>
    /// UI element discovery service.
    /// </summary>
    public IElementDiscovery? ElementDiscovery { get; set; }

    /// <summary>
    /// Screen capture service.
    /// </summary>
    public IScreenCapture? ScreenCapture { get; set; }

    /// <summary>
    /// OCR engine service.
    /// </summary>
    public IOcrEngine? OcrEngine { get; set; }

    /// <summary>
    /// Variables available to the script.
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>
    /// Maximum execution time before timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Cancellation token for execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    /// Security policy for this execution.
    /// </summary>
    public SecurityPolicy SecurityPolicy { get; set; } = SecurityPolicy.Default;

    /// <summary>
    /// Logging function for info messages.
    /// </summary>
    public Action<string>? LogInfo { get; set; }

    /// <summary>
    /// Logging function for warning messages.
    /// </summary>
    public Action<string>? LogWarning { get; set; }

    /// <summary>
    /// Logging function for error messages.
    /// </summary>
    public Action<string, Exception>? LogError { get; set; }
}

