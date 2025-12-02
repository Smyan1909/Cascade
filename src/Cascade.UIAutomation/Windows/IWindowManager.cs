using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Windows;

/// <summary>
/// Provides window management operations.
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Brings a window to the foreground.
    /// </summary>
    Task<bool> SetForegroundAsync(IUIElement window);

    /// <summary>
    /// Minimizes a window.
    /// </summary>
    Task MinimizeAsync(IUIElement window);

    /// <summary>
    /// Maximizes a window.
    /// </summary>
    Task MaximizeAsync(IUIElement window);

    /// <summary>
    /// Restores a window to its normal state.
    /// </summary>
    Task RestoreAsync(IUIElement window);

    /// <summary>
    /// Closes a window.
    /// </summary>
    Task CloseAsync(IUIElement window);

    /// <summary>
    /// Moves a window to the specified position.
    /// </summary>
    Task MoveAsync(IUIElement window, int x, int y);

    /// <summary>
    /// Resizes a window.
    /// </summary>
    Task ResizeAsync(IUIElement window, int width, int height);

    /// <summary>
    /// Attaches to a process by process ID.
    /// </summary>
    IUIElement? AttachToProcess(int processId);

    /// <summary>
    /// Attaches to a process by process name.
    /// </summary>
    IUIElement? AttachToProcess(string processName);

    /// <summary>
    /// Launches an application and attaches to it.
    /// </summary>
    Task<IUIElement?> LaunchAndAttachAsync(string executablePath, string? arguments = null, TimeSpan? timeout = null);

    /// <summary>
    /// Waits for a window to be ready for input.
    /// </summary>
    Task<bool> WaitForInputIdleAsync(IUIElement window, TimeSpan timeout);
}

