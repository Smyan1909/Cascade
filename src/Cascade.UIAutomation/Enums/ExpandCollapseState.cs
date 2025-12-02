namespace Cascade.UIAutomation.Enums;

/// <summary>
/// The expand/collapse state of a UI element.
/// </summary>
public enum ExpandCollapseState
{
    /// <summary>
    /// The element is collapsed and no child elements are visible.
    /// </summary>
    Collapsed = 0,

    /// <summary>
    /// The element is expanded and all child elements are visible.
    /// </summary>
    Expanded = 1,

    /// <summary>
    /// The element is partially expanded and some child elements are visible.
    /// </summary>
    PartiallyExpanded = 2,

    /// <summary>
    /// The element does not expand or collapse.
    /// </summary>
    LeafNode = 3
}

