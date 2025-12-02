using Cascade.UIAutomation.Actions;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Interop;
using Cascade.UIAutomation.TreeWalker;
using Cascade.UIAutomation.Windows;

namespace Cascade.UIAutomation.Services;

/// <summary>
/// Main service facade for UI Automation operations.
/// </summary>
public class UIAutomationService : IDisposable
{
    private readonly IUIAutomationWrapper _automation;
    private readonly ElementFactory _factory;
    private readonly ElementCache? _cache;
    private readonly UIAutomationOptions _options;
    private bool _disposed;

    /// <summary>
    /// Gets the element discovery service.
    /// </summary>
    public IElementDiscovery Discovery { get; }

    /// <summary>
    /// Gets the tree walker service.
    /// </summary>
    public ITreeWalker TreeWalker { get; }

    /// <summary>
    /// Gets the action executor service.
    /// </summary>
    public IActionExecutor Actions { get; }

    /// <summary>
    /// Gets the window manager service.
    /// </summary>
    public IWindowManager Windows { get; }

    /// <summary>
    /// Gets the element cache (null if caching is disabled).
    /// </summary>
    public ElementCache? Cache => _cache;

    /// <summary>
    /// Gets the configuration options.
    /// </summary>
    public UIAutomationOptions Options => _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAutomationService"/> class with default options.
    /// </summary>
    public UIAutomationService() : this(new UIAutomationOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAutomationService"/> class with specified options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public UIAutomationService(UIAutomationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _automation = new UIAutomationWrapper();

        // Initialize cache if enabled
        if (options.EnableCaching)
        {
            _cache = new ElementCache
            {
                DefaultCacheDuration = options.CacheDuration,
                MaxCachedElements = options.MaxCachedElements
            };
        }

        // Initialize factory
        _factory = new ElementFactory(_automation, _cache);

        // Initialize services
        Discovery = new ElementDiscovery(_automation, _factory);
        TreeWalker = new UITreeWalker(_automation, _factory,
            options.UseControlView ? TreeViewType.Control : TreeViewType.Raw);
        Actions = new ActionExecutor(options.DefaultClickDelay, options.DefaultTypeDelay);
        Windows = new WindowManager(Discovery);
    }

    /// <summary>
    /// Initializes a new instance with custom dependencies (for testing).
    /// </summary>
    internal UIAutomationService(
        IUIAutomationWrapper automation,
        UIAutomationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));

        if (options.EnableCaching)
        {
            _cache = new ElementCache
            {
                DefaultCacheDuration = options.CacheDuration,
                MaxCachedElements = options.MaxCachedElements
            };
        }

        _factory = new ElementFactory(_automation, _cache);
        Discovery = new ElementDiscovery(_automation, _factory);
        TreeWalker = new UITreeWalker(_automation, _factory,
            options.UseControlView ? TreeViewType.Control : TreeViewType.Raw);
        Actions = new ActionExecutor(options.DefaultClickDelay, options.DefaultTypeDelay);
        Windows = new WindowManager(Discovery);
    }

    /// <summary>
    /// Gets the desktop root element.
    /// </summary>
    public IUIElement GetDesktopRoot()
    {
        return Discovery.GetDesktopRoot();
    }

    /// <summary>
    /// Gets the currently focused element.
    /// </summary>
    public IUIElement? GetFocusedElement()
    {
        return Discovery.GetFocusedElement();
    }

    /// <summary>
    /// Gets the foreground window.
    /// </summary>
    public IUIElement? GetForegroundWindow()
    {
        return Discovery.GetForegroundWindow();
    }

    /// <summary>
    /// Finds a window by title.
    /// </summary>
    public IUIElement? FindWindow(string title)
    {
        return Discovery.FindWindow(title);
    }

    /// <summary>
    /// Waits for a window to appear.
    /// </summary>
    public Task<IUIElement?> WaitForWindowAsync(string title, TimeSpan? timeout = null)
    {
        var criteria = SearchCriteria.ByName(title)
            .And(SearchCriteria.ByControlType(Enums.ControlType.Window));
        return Discovery.WaitForElementAsync(criteria, timeout ?? _options.DefaultTimeout);
    }

    /// <summary>
    /// Launches an application and attaches to its main window.
    /// </summary>
    public Task<IUIElement?> LaunchApplicationAsync(string executablePath, string? arguments = null)
    {
        return Windows.LaunchAndAttachAsync(executablePath, arguments, _options.DefaultTimeout);
    }

    /// <summary>
    /// Captures a snapshot of the UI tree.
    /// </summary>
    public TreeSnapshot CaptureSnapshot(IUIElement root, int? maxDepth = null)
    {
        return TreeWalker.CaptureSnapshot(root, maxDepth ?? _options.MaxTreeDepth);
    }

    /// <summary>
    /// Invalidates the element cache.
    /// </summary>
    public void InvalidateCache()
    {
        _cache?.InvalidateAll();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _automation.Dispose();
            _disposed = true;
        }
    }
}

