using Cascade.UIAutomation.Discovery;

namespace Cascade.UIAutomation.Interop;

/// <summary>
/// Wrapper interface for UI Automation operations.
/// Abstracts the COM interop details for easier testing and usage.
/// </summary>
public interface IUIAutomationWrapper : IDisposable
{
    /// <summary>
    /// Gets the root element (desktop).
    /// </summary>
    object GetRootElement();

    /// <summary>
    /// Gets the element that currently has focus.
    /// </summary>
    object? GetFocusedElement();

    /// <summary>
    /// Gets an element from a point on the screen.
    /// </summary>
    object? ElementFromPoint(int x, int y);

    /// <summary>
    /// Gets an element from a window handle.
    /// </summary>
    object? ElementFromHandle(IntPtr hwnd);

    /// <summary>
    /// Creates a condition from search criteria.
    /// </summary>
    object CreateCondition(SearchCriteria criteria);

    /// <summary>
    /// Creates a true condition (matches all elements).
    /// </summary>
    object CreateTrueCondition();

    /// <summary>
    /// Creates a control view tree walker.
    /// </summary>
    object CreateControlViewWalker();

    /// <summary>
    /// Creates a content view tree walker.
    /// </summary>
    object CreateContentViewWalker();

    /// <summary>
    /// Creates a raw view tree walker.
    /// </summary>
    object CreateRawViewWalker();

    /// <summary>
    /// Creates a filtered tree walker with a custom condition.
    /// </summary>
    object CreateFilteredTreeWalker(object condition);

    #region Element Operations

    /// <summary>
    /// Gets a property value from an element.
    /// </summary>
    object? GetPropertyValue(object element, int propertyId);

    /// <summary>
    /// Gets the parent element.
    /// </summary>
    object? GetParent(object element, object treeWalker);

    /// <summary>
    /// Gets the first child element.
    /// </summary>
    object? GetFirstChild(object element, object treeWalker);

    /// <summary>
    /// Gets the last child element.
    /// </summary>
    object? GetLastChild(object element, object treeWalker);

    /// <summary>
    /// Gets the next sibling element.
    /// </summary>
    object? GetNextSibling(object element, object treeWalker);

    /// <summary>
    /// Gets the previous sibling element.
    /// </summary>
    object? GetPreviousSibling(object element, object treeWalker);

    /// <summary>
    /// Finds the first element matching the condition.
    /// </summary>
    object? FindFirst(object element, TreeScope scope, object condition);

    /// <summary>
    /// Finds all elements matching the condition.
    /// </summary>
    object[] FindAll(object element, TreeScope scope, object condition);

    /// <summary>
    /// Gets a pattern from an element.
    /// </summary>
    object? GetPattern(object element, int patternId);

    /// <summary>
    /// Sets focus to an element.
    /// </summary>
    void SetFocus(object element);

    /// <summary>
    /// Gets the runtime ID of an element.
    /// </summary>
    int[]? GetRuntimeId(object element);

    #endregion
}

/// <summary>
/// Specifies the scope for tree operations.
/// </summary>
public enum TreeScope
{
    /// <summary>
    /// The element itself.
    /// </summary>
    Element = 1,

    /// <summary>
    /// The children of the element.
    /// </summary>
    Children = 2,

    /// <summary>
    /// The descendants of the element.
    /// </summary>
    Descendants = 4,

    /// <summary>
    /// The parent of the element.
    /// </summary>
    Parent = 8,

    /// <summary>
    /// The ancestors of the element.
    /// </summary>
    Ancestors = 16,

    /// <summary>
    /// The subtree rooted at the element.
    /// </summary>
    Subtree = Element | Children | Descendants
}

