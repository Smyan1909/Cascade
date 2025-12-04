using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Discovery;

public interface IElementDiscovery
{
    IUIElement GetDesktopRoot();

    IUIElement? GetForegroundWindow();
    IUIElement? FindWindow(string title);
    IUIElement? FindWindow(Func<IUIElement, bool> predicate);
    IReadOnlyList<IUIElement> GetAllWindows();

    IUIElement? GetMainWindow(int processId);
    IUIElement? GetMainWindow(string processName);

    IUIElement? FindElement(SearchCriteria criteria, TimeSpan? timeout = null);
    IReadOnlyList<IUIElement> FindAllElements(SearchCriteria criteria);

    Task<IUIElement?> WaitForElementAsync(SearchCriteria criteria, TimeSpan timeout);
    Task<bool> WaitForElementGoneAsync(SearchCriteria criteria, TimeSpan timeout);
}


