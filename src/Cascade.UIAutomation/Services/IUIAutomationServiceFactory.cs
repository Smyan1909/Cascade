using Cascade.UIAutomation.Session;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Services;

public interface IUIAutomationServiceFactory
{
    IUIAutomationService Create(SessionHandle handle, AutomationElement rootElement, VirtualInputChannel inputChannel);
}


