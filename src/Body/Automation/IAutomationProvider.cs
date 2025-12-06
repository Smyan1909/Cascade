using Cascade.Proto;
using ActionProto = Cascade.Proto.Action;
using StatusProto = Cascade.Proto.Status;

namespace Cascade.Body.Automation;

public interface IAutomationProvider
{
    PlatformSource Platform { get; }

    Task<StatusProto> StartAppAsync(string appName, CancellationToken cancellationToken);

    Task<SemanticTree> GetSemanticTreeAsync(CancellationToken cancellationToken);

    Task<StatusProto> PerformActionAsync(ActionProto action, CancellationToken cancellationToken);

    Task<Screenshot> GetMarkedScreenshotAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns true if the provider can satisfy the selector using platform-native patterns (no keyboard/mouse fallback).
    /// </summary>
    bool SupportsPatternFirst(Selector selector);
}

