namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to controls that have an intrinsic value that does not span a range.
/// </summary>
public interface IValuePattern
{
    /// <summary>
    /// Gets the current value of the control.
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Gets whether the value is read-only.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Sets the value of the control.
    /// </summary>
    Task SetValueAsync(string value);
}

