using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Input;

namespace Cascade.UIAutomation.Actions;

public sealed class TypeAction : IActionExecutor
{
    private readonly IVirtualInputProvider _inputProvider;
    private readonly string _text;

    public TypeAction(IVirtualInputProvider inputProvider, string text)
    {
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public async Task ExecuteAsync(IUIElement element, CancellationToken cancellationToken = default)
    {
        await element.SetFocusAsync().ConfigureAwait(false);
        await _inputProvider.TypeTextAsync(_text, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}


