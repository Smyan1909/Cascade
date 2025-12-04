using Cascade.UIAutomation.Actions;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Models;
using Cascade.UIAutomation.Patterns;
using Cascade.UIAutomation.Session;
using System.Drawing;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Elements;

public interface IUIElement
{
    SessionHandle Session { get; }
    VirtualInputChannel InputChannel { get; }

    string AutomationId { get; }
    string Name { get; }
    string ClassName { get; }
    ControlType ControlType { get; }
    string RuntimeId { get; }
    int ProcessId { get; }

    IUIElement? Parent { get; }
    IReadOnlyList<IUIElement> Children { get; }
    IUIElement? FindFirst(SearchCriteria criteria);
    IReadOnlyList<IUIElement> FindAll(SearchCriteria criteria);

    Rectangle BoundingRectangle { get; }
    Point ClickablePoint { get; }
    bool IsOffscreen { get; }

    bool IsEnabled { get; }
    bool HasKeyboardFocus { get; }
    bool IsContentElement { get; }
    bool IsControlElement { get; }

    bool TryGetPattern<T>(out T pattern) where T : class;
    IReadOnlyList<PatternType> SupportedPatterns { get; }

    Task ClickAsync(ClickType clickType = ClickType.Left);
    Task DoubleClickAsync();
    Task RightClickAsync();
    Task TypeTextAsync(string text);
    Task SetValueAsync(string value);
    Task InvokeAsync();
    Task SetFocusAsync();

    ElementSnapshot ToSnapshot();
}


