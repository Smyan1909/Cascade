using Cascade.CodeGen.Generation;
using Cascade.UIAutomation.Elements;

namespace Cascade.CodeGen.Execution;

public interface IGeneratedActionExecutor
{
    Task ExecuteAsync(ActionRuntimeRequest action, IUIElement element, AutomationCallContext callContext, CancellationToken cancellationToken = default);
}

