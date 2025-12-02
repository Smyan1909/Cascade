namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to controls that manage a value within a range.
/// </summary>
public interface IRangeValuePattern
{
    /// <summary>
    /// Gets the current value.
    /// </summary>
    double Value { get; }

    /// <summary>
    /// Gets whether the value is read-only.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets the maximum value.
    /// </summary>
    double Maximum { get; }

    /// <summary>
    /// Gets the minimum value.
    /// </summary>
    double Minimum { get; }

    /// <summary>
    /// Gets the small change value.
    /// </summary>
    double SmallChange { get; }

    /// <summary>
    /// Gets the large change value.
    /// </summary>
    double LargeChange { get; }

    /// <summary>
    /// Sets the value.
    /// </summary>
    Task SetValueAsync(double value);
}

