using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Input;

namespace Cascade.UIAutomation.Actions;

public sealed class DragDropAction : IActionExecutor
{
    private readonly IVirtualInputProvider _inputProvider;
    private readonly Point _targetPoint;

    public DragDropAction(IVirtualInputProvider inputProvider, Point targetPoint)
    {
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _targetPoint = targetPoint;
    }

    public async Task ExecuteAsync(IUIElement element, CancellationToken cancellationToken = default)
    {
        var start = element.ClickablePoint;
        await _inputProvider.MoveMouseAsync(start, cancellationToken).ConfigureAwait(false);
        await _inputProvider.ClickAsync(MouseButton.Left, new ClickOptions { ClickCount = 1 }, cancellationToken).ConfigureAwait(false);
        await _inputProvider.MoveMouseAsync(_targetPoint, cancellationToken).ConfigureAwait(false);
        await _inputProvider.ClickAsync(MouseButton.Left, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}


