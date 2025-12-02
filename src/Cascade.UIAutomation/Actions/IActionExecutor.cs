using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Actions;

/// <summary>
/// Interface for executing UI actions.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Clicks an element.
    /// </summary>
    Task ClickAsync(IUIElement element, ClickType clickType = ClickType.Left);

    /// <summary>
    /// Double-clicks an element.
    /// </summary>
    Task DoubleClickAsync(IUIElement element);

    /// <summary>
    /// Right-clicks an element.
    /// </summary>
    Task RightClickAsync(IUIElement element);

    /// <summary>
    /// Types text into an element.
    /// </summary>
    Task TypeTextAsync(IUIElement element, string text);

    /// <summary>
    /// Sets the value of an element.
    /// </summary>
    Task SetValueAsync(IUIElement element, string value);

    /// <summary>
    /// Scrolls an element.
    /// </summary>
    Task ScrollAsync(IUIElement element, ScrollAmount horizontal, ScrollAmount vertical);

    /// <summary>
    /// Performs a drag and drop operation.
    /// </summary>
    Task DragDropAsync(IUIElement source, IUIElement target);

    /// <summary>
    /// Performs a drag and drop to specific coordinates.
    /// </summary>
    Task DragDropAsync(IUIElement source, int targetX, int targetY);

    /// <summary>
    /// Sets focus to an element.
    /// </summary>
    Task SetFocusAsync(IUIElement element);

    /// <summary>
    /// Invokes an element's default action.
    /// </summary>
    Task InvokeAsync(IUIElement element);

    /// <summary>
    /// Toggles an element's state.
    /// </summary>
    Task ToggleAsync(IUIElement element);

    /// <summary>
    /// Expands an element.
    /// </summary>
    Task ExpandAsync(IUIElement element);

    /// <summary>
    /// Collapses an element.
    /// </summary>
    Task CollapseAsync(IUIElement element);

    /// <summary>
    /// Selects an element.
    /// </summary>
    Task SelectAsync(IUIElement element);
}

