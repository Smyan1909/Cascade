using System.Collections.Concurrent;
using Cascade.UIAutomation.Elements;

namespace Cascade.Grpc.Server.Sessions;

public sealed class UiElementRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WeakReference<IUIElement>>> _registry = new(StringComparer.Ordinal);

    public void Track(string sessionId, IUIElement element)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || element is null || string.IsNullOrWhiteSpace(element.RuntimeId))
        {
            return;
        }

        var sessionMap = _registry.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, WeakReference<IUIElement>>(StringComparer.Ordinal));
        sessionMap[element.RuntimeId] = new WeakReference<IUIElement>(element);
    }

    public bool TryResolve(string sessionId, string runtimeId, out IUIElement? element)
    {
        element = null;
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(runtimeId))
        {
            return false;
        }

        if (!_registry.TryGetValue(sessionId, out var sessionMap))
        {
            return false;
        }

        if (!sessionMap.TryGetValue(runtimeId, out var reference))
        {
            return false;
        }

        if (reference.TryGetTarget(out var value))
        {
            element = value;
            return true;
        }

        sessionMap.TryRemove(runtimeId, out _);
        return false;
    }

    public void InvalidateSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        _registry.TryRemove(sessionId, out _);
    }
}

