using System.Drawing;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Elements;

/// <summary>
/// Represents a UI Automation element with properties and actions.
/// </summary>
public interface IUIElement
{
    #region Identity Properties

    /// <summary>
    /// Gets the automation ID of the element.
    /// </summary>
    string AutomationId { get; }

    /// <summary>
    /// Gets the name of the element.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the class name of the element.
    /// </summary>
    string ClassName { get; }

    /// <summary>
    /// Gets the control type of the element.
    /// </summary>
    ControlType ControlType { get; }

    /// <summary>
    /// Gets the runtime ID of the element as a string.
    /// </summary>
    string RuntimeId { get; }

    /// <summary>
    /// Gets the process ID of the element's owning process.
    /// </summary>
    int ProcessId { get; }

    #endregion

    #region Hierarchy

    /// <summary>
    /// Gets the parent element, or null if this is the root.
    /// </summary>
    IUIElement? Parent { get; }

    /// <summary>
    /// Gets the child elements.
    /// </summary>
    IReadOnlyList<IUIElement> Children { get; }

    /// <summary>
    /// Finds the first descendant element matching the criteria.
    /// </summary>
    IUIElement? FindFirst(SearchCriteria criteria);

    /// <summary>
    /// Finds all descendant elements matching the criteria.
    /// </summary>
    IReadOnlyList<IUIElement> FindAll(SearchCriteria criteria);

    #endregion

    #region Geometry

    /// <summary>
    /// Gets the bounding rectangle of the element in screen coordinates.
    /// </summary>
    Rectangle BoundingRectangle { get; }

    /// <summary>
    /// Gets a clickable point within the element.
    /// </summary>
    Point ClickablePoint { get; }

    /// <summary>
    /// Gets whether the element is offscreen.
    /// </summary>
    bool IsOffscreen { get; }

    #endregion

    #region State

    /// <summary>
    /// Gets whether the element is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets whether the element has keyboard focus.
    /// </summary>
    bool HasKeyboardFocus { get; }

    /// <summary>
    /// Gets whether the element is a content element.
    /// </summary>
    bool IsContentElement { get; }

    /// <summary>
    /// Gets whether the element is a control element.
    /// </summary>
    bool IsControlElement { get; }

    #endregion

    #region Patterns

    /// <summary>
    /// Attempts to get a pattern interface from this element.
    /// </summary>
    /// <typeparam name="T">The pattern interface type.</typeparam>
    /// <param name="pattern">The pattern instance if available.</param>
    /// <returns>True if the pattern is supported, false otherwise.</returns>
    bool TryGetPattern<T>(out T? pattern) where T : class;

    /// <summary>
    /// Gets the list of supported patterns.
    /// </summary>
    IReadOnlyList<PatternType> SupportedPatterns { get; }

    #endregion

    #region Actions

    /// <summary>
    /// Clicks the element.
    /// </summary>
    /// <param name="clickType">The type of click to perform.</param>
    Task ClickAsync(ClickType clickType = ClickType.Left);

    /// <summary>
    /// Double-clicks the element.
    /// </summary>
    Task DoubleClickAsync();

    /// <summary>
    /// Right-clicks the element.
    /// </summary>
    Task RightClickAsync();

    /// <summary>
    /// Types text into the element using keyboard simulation.
    /// </summary>
    /// <param name="text">The text to type.</param>
    Task TypeTextAsync(string text);

    /// <summary>
    /// Sets the value of the element using the Value pattern.
    /// </summary>
    /// <param name="value">The value to set.</param>
    Task SetValueAsync(string value);

    /// <summary>
    /// Invokes the element using the Invoke pattern.
    /// </summary>
    Task InvokeAsync();

    /// <summary>
    /// Sets focus to this element.
    /// </summary>
    Task SetFocusAsync();

    /// <summary>
    /// Scrolls the element into view.
    /// </summary>
    Task ScrollIntoViewAsync();

    #endregion

    #region Serialization

    /// <summary>
    /// Creates a snapshot of this element's current state.
    /// </summary>
    ElementSnapshot ToSnapshot();

    #endregion
}

