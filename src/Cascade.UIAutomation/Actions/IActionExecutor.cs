using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Actions;

public interface IActionExecutor
{
    Task ExecuteAsync(IUIElement element, CancellationToken cancellationToken = default);
}


