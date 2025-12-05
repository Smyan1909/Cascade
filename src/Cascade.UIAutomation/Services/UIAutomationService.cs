using Cascade.UIAutomation.Actions;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Input;
using Cascade.UIAutomation.Session;
using Cascade.UIAutomation.TreeWalker;
using Cascade.UIAutomation.Windows;
using Microsoft.Extensions.Logging;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Services;

public sealed class UIAutomationService : IUIAutomationService
{
    private readonly SessionContext _sessionContext;
    private readonly ILogger<UIAutomationService>? _logger;

    public UIAutomationService(
        SessionContext sessionContext,
        UIAutomationOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        var nativeInput = new NativeInput();
        var keyboard = new VirtualKeyboard(nativeInput, loggerFactory?.CreateLogger<VirtualKeyboard>());
        var inputProvider = new VirtualMouse(sessionContext.Session, keyboard, nativeInput, loggerFactory?.CreateLogger<VirtualMouse>());
        var elementFactory = new ElementFactory(sessionContext, inputProvider, loggerFactory?.CreateLogger<ElementFactory>());
        elementFactory.Cache.DefaultCacheDuration = options.CacheDuration;
        elementFactory.Cache.EnableCaching = options.EnableCaching;
        var discovery = new ElementDiscovery(sessionContext, elementFactory, options, loggerFactory?.CreateLogger<ElementDiscovery>());
        var walker = new UITreeWalker(elementFactory, System.Windows.Automation.TreeWalker.ControlViewWalker, loggerFactory?.CreateLogger<UITreeWalker>());
        var windowManager = new WindowManager(discovery, loggerFactory?.CreateLogger<WindowManager>());

        Discovery = discovery;
        TreeWalker = walker;
        WindowManager = windowManager;
        InputProvider = inputProvider;

        _logger = loggerFactory?.CreateLogger<UIAutomationService>();
    }

    public IElementDiscovery Discovery { get; }
    public ITreeWalker TreeWalker { get; }
    public IWindowManager WindowManager { get; }
    public IVirtualInputProvider InputProvider { get; }

    public async Task ExecuteAsync(Func<IElementDiscovery, Task> callback, CancellationToken cancellationToken = default)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));

        try
        {
            await callback(Discovery).ConfigureAwait(false);
        }
        catch (ElementNotAvailableException ex)
        {
            _logger?.LogWarning(ex, "Element became unavailable during execution.");
            throw new UIAutomationException("Element became unavailable.", UIAutomationErrorCode.ElementNotFound, innerException: ex);
        }
    }

    public Task<IUIElement?> FindElementAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var element = Discovery.FindElement(criteria);
        return Task.FromResult(element);
    }

    public async Task PerformActionAsync(IUIElement element, IActionExecutor action, CancellationToken cancellationToken = default)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (action is null) throw new ArgumentNullException(nameof(action));

        await action.ExecuteAsync(element, cancellationToken).ConfigureAwait(false);
    }
}


