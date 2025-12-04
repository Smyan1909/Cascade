using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Input;

namespace Cascade.UIAutomation.Actions;

public sealed class ClickAction : IActionExecutor
{
    private readonly IVirtualInputProvider _inputProvider;
    private readonly ClickType _clickType;

    public ClickAction(IVirtualInputProvider inputProvider, ClickType clickType = ClickType.Left)
    {
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _clickType = clickType;
    }

    public async Task ExecuteAsync(IUIElement element, CancellationToken cancellationToken = default)
    {
        var point = element.ClickablePoint;
        await _inputProvider.MoveMouseAsync(point, cancellationToken).ConfigureAwait(false);

        var button = _clickType switch
        {
            ClickType.Left => MouseButton.Left,
            ClickType.Right => MouseButton.Right,
            ClickType.Middle => MouseButton.Middle,
            _ => MouseButton.Left
        };

        await _inputProvider.ClickAsync(button, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}


