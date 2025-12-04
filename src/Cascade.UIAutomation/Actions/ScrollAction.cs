using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Input;

namespace Cascade.UIAutomation.Actions;

public sealed class ScrollAction : IActionExecutor
{
    private readonly IVirtualInputProvider _inputProvider;
    private readonly int _delta;
    private readonly bool _horizontal;

    public ScrollAction(IVirtualInputProvider inputProvider, int delta, bool horizontal = false)
    {
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _delta = delta;
        _horizontal = horizontal;
    }

    public async Task ExecuteAsync(IUIElement element, CancellationToken cancellationToken = default)
    {
        await element.SetFocusAsync().ConfigureAwait(false);
        await _inputProvider.ScrollAsync(_delta, new ScrollOptions { Horizontal = _horizontal }, cancellationToken).ConfigureAwait(false);
    }
}


