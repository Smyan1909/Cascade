using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to controls that can cycle through states.
/// </summary>
public interface ITogglePattern
{
    /// <summary>
    /// Gets the current toggle state.
    /// </summary>
    ToggleState ToggleState { get; }

    /// <summary>
    /// Cycles through the toggle states.
    /// </summary>
    Task ToggleAsync();
}

