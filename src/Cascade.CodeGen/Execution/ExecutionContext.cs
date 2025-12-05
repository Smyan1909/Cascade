using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.Vision.Capture;
using Cascade.Vision.OCR;

namespace Cascade.CodeGen.Execution;

public sealed class ExecutionContext
{
    public Guid ExecutionId { get; } = Guid.NewGuid();
    public AutomationCallContext? CallContext { get; set; }
    public IElementDiscovery? ElementDiscovery { get; set; }
    public IGeneratedActionExecutor? ActionExecutor { get; set; }
    public IScreenCapture? ScreenCapture { get; set; }
    public IOcrEngine? OcrEngine { get; set; }
    public IServiceProvider? Services { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    public SecurityPolicy SecurityPolicy { get; set; } = SecurityPolicy.Default;
    public Action<string>? LogInfo { get; set; }
    public Action<string>? LogWarning { get; set; }
    public Action<string, Exception>? LogError { get; set; }

    public void Log(string message)
    {
        LogInfo?.Invoke(message);
    }

    public void Log(string message, Exception ex)
    {
        LogError?.Invoke(message, ex);
    }
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public object? ReturnValue { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public Guid ExecutionId { get; set; }
    public IReadOnlyList<string> Logs { get; set; } = Array.Empty<string>();
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Completed;
}

public sealed class ExecutionResult<T> : ExecutionResult
{
    public new T? ReturnValue { get; set; }
}

public enum ExecutionStatus
{
    Completed,
    Failed,
    Timeout,
    Cancelled,
    SecurityViolation
}

