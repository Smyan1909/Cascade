using Cascade.UIAutomation.Actions;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Input;
using Cascade.UIAutomation.TreeWalker;
using Cascade.UIAutomation.Windows;

namespace Cascade.UIAutomation.Services;

public interface IUIAutomationService
{
    IElementDiscovery Discovery { get; }
    ITreeWalker TreeWalker { get; }
    IWindowManager WindowManager { get; }
    IVirtualInputProvider InputProvider { get; }

    Task ExecuteAsync(Func<IElementDiscovery, Task> callback, CancellationToken cancellationToken = default);
    Task<IUIElement?> FindElementAsync(SearchCriteria criteria, CancellationToken cancellationToken = default);
    Task PerformActionAsync(IUIElement element, IActionExecutor action, CancellationToken cancellationToken = default);
}


