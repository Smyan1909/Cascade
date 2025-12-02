using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.TreeWalker;

/// <summary>
/// Provides methods for navigating the UI element tree.
/// </summary>
public interface ITreeWalker
{
    /// <summary>
    /// Gets the parent of an element.
    /// </summary>
    IUIElement? GetParent(IUIElement element);

    /// <summary>
    /// Gets the first child of an element.
    /// </summary>
    IUIElement? GetFirstChild(IUIElement element);

    /// <summary>
    /// Gets the last child of an element.
    /// </summary>
    IUIElement? GetLastChild(IUIElement element);

    /// <summary>
    /// Gets the next sibling of an element.
    /// </summary>
    IUIElement? GetNextSibling(IUIElement element);

    /// <summary>
    /// Gets the previous sibling of an element.
    /// </summary>
    IUIElement? GetPreviousSibling(IUIElement element);

    /// <summary>
    /// Gets all children of an element.
    /// </summary>
    IEnumerable<IUIElement> GetChildren(IUIElement element);

    /// <summary>
    /// Gets all descendants of an element up to a maximum depth.
    /// </summary>
    /// <param name="element">The root element.</param>
    /// <param name="maxDepth">The maximum depth to traverse (-1 for unlimited).</param>
    IEnumerable<IUIElement> GetDescendants(IUIElement element, int maxDepth = -1);

    /// <summary>
    /// Gets all ancestors of an element.
    /// </summary>
    IEnumerable<IUIElement> GetAncestors(IUIElement element);

    /// <summary>
    /// Creates a new tree walker with a custom filter.
    /// </summary>
    ITreeWalker WithFilter(Func<IUIElement, bool> filter);

    /// <summary>
    /// Gets the control view walker (excludes non-interactive elements).
    /// </summary>
    ITreeWalker ControlViewWalker { get; }

    /// <summary>
    /// Gets the content view walker (only content-relevant elements).
    /// </summary>
    ITreeWalker ContentViewWalker { get; }

    /// <summary>
    /// Gets the raw view walker (all elements).
    /// </summary>
    ITreeWalker RawViewWalker { get; }

    /// <summary>
    /// Captures a snapshot of the element tree.
    /// </summary>
    /// <param name="root">The root element for the snapshot.</param>
    /// <param name="maxDepth">The maximum depth to capture (-1 for unlimited).</param>
    TreeSnapshot CaptureSnapshot(IUIElement root, int maxDepth = -1);
}

