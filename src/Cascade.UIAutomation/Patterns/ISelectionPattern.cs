using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to a container that manages a related set of child controls.
/// </summary>
public interface ISelectionPattern
{
    /// <summary>
    /// Gets the currently selected elements.
    /// </summary>
    IReadOnlyList<IUIElement> GetSelection();

    /// <summary>
    /// Gets whether multiple selection is allowed.
    /// </summary>
    bool CanSelectMultiple { get; }

    /// <summary>
    /// Gets whether selection is required.
    /// </summary>
    bool IsSelectionRequired { get; }
}

/// <summary>
/// Provides access to child controls of containers that implement ISelectionPattern.
/// </summary>
public interface ISelectionItemPattern
{
    /// <summary>
    /// Gets whether the item is selected.
    /// </summary>
    bool IsSelected { get; }

    /// <summary>
    /// Gets the selection container.
    /// </summary>
    IUIElement? SelectionContainer { get; }

    /// <summary>
    /// Selects this item, deselecting all other items.
    /// </summary>
    Task SelectAsync();

    /// <summary>
    /// Adds this item to the selection.
    /// </summary>
    Task AddToSelectionAsync();

    /// <summary>
    /// Removes this item from the selection.
    /// </summary>
    Task RemoveFromSelectionAsync();
}

