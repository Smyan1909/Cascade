using System.Windows.Automation;

namespace Cascade.UIAutomation.Session;

public interface ISessionContextAccessor
{
    SessionHandle Session { get; }
    VirtualInputChannel InputChannel { get; }
    AutomationElement RootElement { get; }
}


