using System.Windows.Automation;

namespace Cascade.UIAutomation.Session;

/// <summary>
/// Provides ambient data for UI automation calls scoped to a session.
/// </summary>
public sealed class SessionContext
{
    public SessionContext(SessionHandle session, VirtualInputChannel inputChannel, AutomationElement rootElement)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        InputChannel = inputChannel ?? throw new ArgumentNullException(nameof(inputChannel));
        RootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));

        Session.EnsureValid();
        if (!InputChannel.IsValid)
        {
            throw new InvalidOperationException("Input channel is not valid.");
        }
    }

    public SessionHandle Session { get; }
    public VirtualInputChannel InputChannel { get; }
    public AutomationElement RootElement { get; }
}


