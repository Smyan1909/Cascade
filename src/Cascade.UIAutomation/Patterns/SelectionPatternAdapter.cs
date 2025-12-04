using Cascade.UIAutomation.Elements;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Patterns;

internal sealed class SelectionPatternAdapter : ISelectionPattern
{
    private readonly ElementFactory _factory;

    public SelectionPatternAdapter(SelectionPattern nativePattern, ElementFactory factory)
    {
        NativePattern = nativePattern ?? throw new ArgumentNullException(nameof(nativePattern));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public SelectionPattern NativePattern { get; }

    public bool CanSelectMultiple => NativePattern.Current.CanSelectMultiple;
    public bool IsSelectionRequired => NativePattern.Current.IsSelectionRequired;

    public IReadOnlyList<IUIElement> GetSelection()
    {
        var selection = NativePattern.Current.GetSelection();
        return selection.Select(_factory.Create).ToList();
    }
}

internal sealed class SelectionItemPatternAdapter : ISelectionItemPattern
{
    private readonly ElementFactory _factory;

    public SelectionItemPatternAdapter(SelectionItemPattern nativePattern, ElementFactory factory)
    {
        NativePattern = nativePattern ?? throw new ArgumentNullException(nameof(nativePattern));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public SelectionItemPattern NativePattern { get; }

    public bool IsSelected => NativePattern.Current.IsSelected;

    public IUIElement SelectionContainer => _factory.Create((AutomationElement)NativePattern.Current.SelectionContainer);

    public Task SelectAsync()
    {
        NativePattern.Select();
        return Task.CompletedTask;
    }

    public Task AddToSelectionAsync()
    {
        NativePattern.AddToSelection();
        return Task.CompletedTask;
    }

    public Task RemoveFromSelectionAsync()
    {
        NativePattern.RemoveFromSelection();
        return Task.CompletedTask;
    }
}


