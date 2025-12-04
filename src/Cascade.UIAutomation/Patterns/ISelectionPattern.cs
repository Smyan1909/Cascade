using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Patterns;

public interface ISelectionPattern : IPatternProvider<System.Windows.Automation.SelectionPattern>
{
    IReadOnlyList<IUIElement> GetSelection();
    bool CanSelectMultiple { get; }
    bool IsSelectionRequired { get; }
}

public interface ISelectionItemPattern : IPatternProvider<System.Windows.Automation.SelectionItemPattern>
{
    bool IsSelected { get; }
    IUIElement SelectionContainer { get; }
    Task SelectAsync();
    Task AddToSelectionAsync();
    Task RemoveFromSelectionAsync();
}


