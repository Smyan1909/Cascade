using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to controls that visually expand to display content and collapse to hide content.
/// </summary>
public interface IExpandCollapsePattern
{
    /// <summary>
    /// Gets the current expand/collapse state.
    /// </summary>
    ExpandCollapseState State { get; }

    /// <summary>
    /// Expands the control.
    /// </summary>
    Task ExpandAsync();

    /// <summary>
    /// Collapses the control.
    /// </summary>
    Task CollapseAsync();
}

