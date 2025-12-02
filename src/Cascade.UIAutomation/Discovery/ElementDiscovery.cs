using System.Diagnostics;
using System.Runtime.InteropServices;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.Exceptions;
using Cascade.UIAutomation.Interop;

namespace Cascade.UIAutomation.Discovery;

/// <summary>
/// Implementation of IElementDiscovery for discovering UI elements.
/// </summary>
public class ElementDiscovery : IElementDiscovery
{
    private readonly IUIAutomationWrapper _automation;
    private readonly ElementFactory _factory;
    private readonly TimeSpan _defaultPollingInterval = TimeSpan.FromMilliseconds(100);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr NativeGetForegroundWindow();

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementDiscovery"/> class.
    /// </summary>
    /// <param name="automation">The UI Automation wrapper.</param>
    /// <param name="factory">The element factory.</param>
    public ElementDiscovery(IUIAutomationWrapper automation, ElementFactory factory)
    {
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public IUIElement GetDesktopRoot()
    {
        var root = _automation.GetRootElement();
        return _factory.Create(root)!;
    }

    /// <inheritdoc />
    public IUIElement? GetFocusedElement()
    {
        var focused = _automation.GetFocusedElement();
        return _factory.Create(focused);
    }

    /// <inheritdoc />
    public IUIElement? GetForegroundWindow()
    {
        var hwnd = NativeGetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        return ElementFromHandle(hwnd);
    }

    /// <inheritdoc />
    public IUIElement? FindWindow(string title)
    {
        if (string.IsNullOrEmpty(title))
            return null;

        var criteria = SearchCriteria.ByName(title)
            .And(SearchCriteria.ByControlType(ControlType.Window));

        return FindElement(criteria);
    }

    /// <inheritdoc />
    public IUIElement? FindWindow(Func<IUIElement, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        var windows = GetAllWindows();
        return windows.FirstOrDefault(predicate);
    }

    /// <inheritdoc />
    public IReadOnlyList<IUIElement> GetAllWindows()
    {
        var root = GetDesktopRoot();
        var criteria = SearchCriteria.ByControlType(ControlType.Window);
        var condition = _automation.CreateCondition(criteria);
        var results = _automation.FindAll(
            ((UIElement)root).NativeElement,
            TreeScope.Children,
            condition);

        return results.Select(e => _factory.Create(e)!).ToList();
    }

    /// <inheritdoc />
    public IUIElement? GetMainWindow(int processId)
    {
        var criteria = SearchCriteria.ByProcessId(processId)
            .And(SearchCriteria.ByControlType(ControlType.Window));

        var root = GetDesktopRoot();
        var condition = _automation.CreateCondition(criteria);
        var result = _automation.FindFirst(
            ((UIElement)root).NativeElement,
            TreeScope.Children,
            condition);

        return _factory.Create(result);
    }

    /// <inheritdoc />
    public IUIElement? GetMainWindow(string processName)
    {
        if (string.IsNullOrEmpty(processName))
            return null;

        var processes = Process.GetProcessesByName(processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase));
        if (processes.Length == 0)
            return null;

        return GetMainWindow(processes[0].Id);
    }

    /// <inheritdoc />
    public IUIElement? FindElement(SearchCriteria criteria, TimeSpan? timeout = null)
    {
        if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
        {
            return WaitForElementAsync(criteria, timeout.Value).GetAwaiter().GetResult();
        }

        var root = GetDesktopRoot();
        return root.FindFirst(criteria);
    }

    /// <inheritdoc />
    public IReadOnlyList<IUIElement> FindAllElements(SearchCriteria criteria)
    {
        var root = GetDesktopRoot();
        return root.FindAll(criteria);
    }

    /// <inheritdoc />
    public async Task<IUIElement?> WaitForElementAsync(SearchCriteria criteria, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var element = FindElement(criteria);
            if (element != null)
                return element;

            await Task.Delay(_defaultPollingInterval);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> WaitForElementGoneAsync(SearchCriteria criteria, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var element = FindElement(criteria);
            if (element == null)
                return true;

            await Task.Delay(_defaultPollingInterval);
        }

        return false;
    }

    /// <inheritdoc />
    public IUIElement? ElementFromPoint(int x, int y)
    {
        var element = _automation.ElementFromPoint(x, y);
        return _factory.Create(element);
    }

    /// <inheritdoc />
    public IUIElement? ElementFromHandle(IntPtr hwnd)
    {
        var element = _automation.ElementFromHandle(hwnd);
        return _factory.Create(element);
    }
}

