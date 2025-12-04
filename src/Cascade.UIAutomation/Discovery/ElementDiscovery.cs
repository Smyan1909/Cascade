using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Services;
using Cascade.UIAutomation.Session;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using System.Linq;

namespace Cascade.UIAutomation.Discovery;

public sealed class ElementDiscovery : IElementDiscovery
{
    private readonly SessionContext _context;
    private readonly ElementFactory _factory;
    private readonly UIAutomationOptions _options;
    private readonly ILogger<ElementDiscovery>? _logger;

    public ElementDiscovery(
        SessionContext context,
        ElementFactory factory,
        UIAutomationOptions options,
        ILogger<ElementDiscovery>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _options = options ?? new UIAutomationOptions();
        _logger = logger;
    }

    public IUIElement GetDesktopRoot() => _factory.Create(_context.RootElement);

    public IUIElement? GetForegroundWindow()
    {
        try
        {
            var condition = new PropertyCondition(AutomationElement.HasKeyboardFocusProperty, true);
            var focused = _context.RootElement.FindFirst(TreeScope.Subtree, condition);
            if (focused is null)
            {
                return null;
            }

            var window = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(focused);
            return window is null ? null : _factory.Create(window);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    public IUIElement? FindWindow(string title)
    {
        return GetAllWindows().FirstOrDefault(window =>
            string.Equals(window.Name, title, StringComparison.OrdinalIgnoreCase));
    }

    public IUIElement? FindWindow(Func<IUIElement, bool> predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return GetAllWindows().FirstOrDefault(predicate);
    }

    public IReadOnlyList<IUIElement> GetAllWindows()
    {
        var windows = _context.RootElement.FindAll(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));

        return _factory.CreateMany(windows);
    }

    public IUIElement? GetMainWindow(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        return GetAllWindows().FirstOrDefault(window => window.ProcessId == processId);
    }

    public IUIElement? GetMainWindow(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var normalized = System.IO.Path.GetFileNameWithoutExtension(processName);
        return GetAllWindows().FirstOrDefault(window =>
        {
            try
            {
                var process = Process.GetProcessById(window.ProcessId);
                return string.Equals(process.ProcessName, normalized, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });
    }

    public IUIElement? FindElement(SearchCriteria criteria, TimeSpan? timeout = null)
    {
        if (criteria is null) throw new ArgumentNullException(nameof(criteria));
        var native = FindElementInternal(criteria, timeout ?? _options.DefaultTimeout);
        return native is null ? null : _factory.Create(native);
    }

    public IReadOnlyList<IUIElement> FindAllElements(SearchCriteria criteria)
    {
        if (criteria is null) throw new ArgumentNullException(nameof(criteria));
        var elements = EnumerateMatches(criteria).Select(_factory.Create).ToList();
        return elements;
    }

    public async Task<IUIElement?> WaitForElementAsync(SearchCriteria criteria, TimeSpan timeout)
    {
        if (criteria is null) throw new ArgumentNullException(nameof(criteria));

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var element = FindElement(criteria, TimeSpan.Zero);
            if (element is not null)
            {
                return element;
            }

            await Task.Delay(_options.ElementWaitPollingInterval).ConfigureAwait(false);
        }

        return null;
    }

    public async Task<bool> WaitForElementGoneAsync(SearchCriteria criteria, TimeSpan timeout)
    {
        if (criteria is null) throw new ArgumentNullException(nameof(criteria));

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var element = FindElement(criteria, TimeSpan.Zero);
            if (element is null)
            {
                return true;
            }

            await Task.Delay(_options.ElementWaitPollingInterval).ConfigureAwait(false);
        }

        return false;
    }

    private AutomationElement? FindElementInternal(SearchCriteria? criteria, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed <= timeout)
        {
            var match = EnumerateMatches(criteria).FirstOrDefault();
            if (match is not null)
            {
                return match;
            }

            Thread.Sleep(_options.ElementWaitPollingInterval);
        }

        _logger?.LogWarning("Element not found after {Timeout}ms using criteria {@Criteria}", timeout.TotalMilliseconds, criteria);
        return null;
    }

    private IEnumerable<AutomationElement> EnumerateMatches(SearchCriteria? criteria)
    {
        var condition = criteria?.ToAutomationCondition() ?? Condition.TrueCondition;
        var results = _context.RootElement.FindAll(TreeScope.Subtree, condition);

        foreach (AutomationElement element in results)
        {
            if (criteria is null || criteria.Match(element))
            {
                yield return element;
            }
        }
    }
}


