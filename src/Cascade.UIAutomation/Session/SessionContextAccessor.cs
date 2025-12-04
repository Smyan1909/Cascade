using System.Threading;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Session;

/// <summary>
/// Ambient holder for the current session context.
/// </summary>
public sealed class SessionContextAccessor : ISessionContextAccessor
{
    private readonly AsyncLocal<SessionContext?> _current = new();

    public SessionHandle Session => RequireContext().Session;
    public VirtualInputChannel InputChannel => RequireContext().InputChannel;
    public AutomationElement RootElement => RequireContext().RootElement;

    public IDisposable Push(SessionContext context)
    {
        var previous = _current.Value;
        _current.Value = context;
        return new PopScope(() => _current.Value = previous);
    }

    private SessionContext RequireContext()
    {
        return _current.Value ?? throw new InvalidOperationException("SessionContext was not provided. Resolve UIAutomationService with a session handle first.");
    }

    private sealed class PopScope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public PopScope(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}


