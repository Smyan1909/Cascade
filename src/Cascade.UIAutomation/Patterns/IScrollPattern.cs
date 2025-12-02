using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to a container that can scroll its content.
/// </summary>
public interface IScrollPattern
{
    /// <summary>
    /// Gets the horizontal scroll percentage (0-100).
    /// </summary>
    double HorizontalScrollPercent { get; }

    /// <summary>
    /// Gets the vertical scroll percentage (0-100).
    /// </summary>
    double VerticalScrollPercent { get; }

    /// <summary>
    /// Gets the horizontal view size as a percentage of the total content.
    /// </summary>
    double HorizontalViewSize { get; }

    /// <summary>
    /// Gets the vertical view size as a percentage of the total content.
    /// </summary>
    double VerticalViewSize { get; }

    /// <summary>
    /// Gets whether the container is horizontally scrollable.
    /// </summary>
    bool HorizontallyScrollable { get; }

    /// <summary>
    /// Gets whether the container is vertically scrollable.
    /// </summary>
    bool VerticallyScrollable { get; }

    /// <summary>
    /// Scrolls the content.
    /// </summary>
    Task ScrollAsync(ScrollAmount horizontal, ScrollAmount vertical);

    /// <summary>
    /// Sets the scroll position.
    /// </summary>
    Task SetScrollPercentAsync(double horizontal, double vertical);
}

/// <summary>
/// Provides access to elements that can be scrolled into view.
/// </summary>
public interface IScrollItemPattern
{
    /// <summary>
    /// Scrolls the element into view.
    /// </summary>
    Task ScrollIntoViewAsync();
}

