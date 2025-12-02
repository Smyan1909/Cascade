using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Discovery;

/// <summary>
/// Provides methods for discovering UI elements.
/// </summary>
public interface IElementDiscovery
{
    /// <summary>
    /// Gets the desktop root element.
    /// </summary>
    IUIElement GetDesktopRoot();

    /// <summary>
    /// Gets the currently focused element.
    /// </summary>
    IUIElement? GetFocusedElement();

    /// <summary>
    /// Gets the foreground window.
    /// </summary>
    IUIElement? GetForegroundWindow();

    /// <summary>
    /// Finds a window by its title.
    /// </summary>
    /// <param name="title">The window title to find.</param>
    /// <returns>The window element, or null if not found.</returns>
    IUIElement? FindWindow(string title);

    /// <summary>
    /// Finds a window using a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match windows against.</param>
    /// <returns>The first matching window, or null if not found.</returns>
    IUIElement? FindWindow(Func<IUIElement, bool> predicate);

    /// <summary>
    /// Gets all top-level windows.
    /// </summary>
    /// <returns>A list of all top-level window elements.</returns>
    IReadOnlyList<IUIElement> GetAllWindows();

    /// <summary>
    /// Gets the main window of a process by process ID.
    /// </summary>
    /// <param name="processId">The process ID.</param>
    /// <returns>The main window element, or null if not found.</returns>
    IUIElement? GetMainWindow(int processId);

    /// <summary>
    /// Gets the main window of a process by process name.
    /// </summary>
    /// <param name="processName">The process name.</param>
    /// <returns>The main window element, or null if not found.</returns>
    IUIElement? GetMainWindow(string processName);

    /// <summary>
    /// Finds the first element matching the criteria.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="timeout">Optional timeout for the search.</param>
    /// <returns>The matching element, or null if not found.</returns>
    IUIElement? FindElement(SearchCriteria criteria, TimeSpan? timeout = null);

    /// <summary>
    /// Finds all elements matching the criteria.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <returns>A list of matching elements.</returns>
    IReadOnlyList<IUIElement> FindAllElements(SearchCriteria criteria);

    /// <summary>
    /// Waits for an element matching the criteria to appear.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>The matching element, or null if timeout.</returns>
    Task<IUIElement?> WaitForElementAsync(SearchCriteria criteria, TimeSpan timeout);

    /// <summary>
    /// Waits for all elements matching the criteria to disappear.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>True if all elements disappeared, false if timeout.</returns>
    Task<bool> WaitForElementGoneAsync(SearchCriteria criteria, TimeSpan timeout);

    /// <summary>
    /// Gets an element from a point on the screen.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <returns>The element at the point, or null if none.</returns>
    IUIElement? ElementFromPoint(int x, int y);

    /// <summary>
    /// Gets an element from a window handle.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <returns>The element, or null if not found.</returns>
    IUIElement? ElementFromHandle(IntPtr hwnd);
}

