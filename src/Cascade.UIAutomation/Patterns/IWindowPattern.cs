namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to window-specific functionality.
/// </summary>
public interface IWindowPattern
{
    /// <summary>
    /// Gets whether the window can be maximized.
    /// </summary>
    bool CanMaximize { get; }

    /// <summary>
    /// Gets whether the window can be minimized.
    /// </summary>
    bool CanMinimize { get; }

    /// <summary>
    /// Gets whether the window is modal.
    /// </summary>
    bool IsModal { get; }

    /// <summary>
    /// Gets whether the window is topmost.
    /// </summary>
    bool IsTopmost { get; }

    /// <summary>
    /// Gets the visual state of the window.
    /// </summary>
    WindowVisualState WindowVisualState { get; }

    /// <summary>
    /// Gets the interaction state of the window.
    /// </summary>
    WindowInteractionState WindowInteractionState { get; }

    /// <summary>
    /// Sets the visual state of the window.
    /// </summary>
    Task SetWindowVisualStateAsync(WindowVisualState state);

    /// <summary>
    /// Closes the window.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Waits for the window to be ready for input.
    /// </summary>
    Task<bool> WaitForInputIdleAsync(int milliseconds);
}

/// <summary>
/// The visual state of a window.
/// </summary>
public enum WindowVisualState
{
    /// <summary>
    /// The window is normal (restored).
    /// </summary>
    Normal = 0,

    /// <summary>
    /// The window is maximized.
    /// </summary>
    Maximized = 1,

    /// <summary>
    /// The window is minimized.
    /// </summary>
    Minimized = 2
}

/// <summary>
/// The interaction state of a window.
/// </summary>
public enum WindowInteractionState
{
    /// <summary>
    /// The window is running and ready for user interaction.
    /// </summary>
    Running = 0,

    /// <summary>
    /// The window is closing.
    /// </summary>
    Closing = 1,

    /// <summary>
    /// The window is ready for input but is blocked by a modal dialog.
    /// </summary>
    ReadyForUserInteraction = 2,

    /// <summary>
    /// The window is blocked by a modal dialog.
    /// </summary>
    BlockedByModalWindow = 3,

    /// <summary>
    /// The window is not responding.
    /// </summary>
    NotResponding = 4
}

