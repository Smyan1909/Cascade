using Cascade.UIAutomation.Interop;

namespace Cascade.UIAutomation.Elements;

/// <summary>
/// Factory for creating UI element wrappers.
/// </summary>
public class ElementFactory
{
    private readonly IUIAutomationWrapper _automation;
    private readonly ElementCache? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementFactory"/> class.
    /// </summary>
    /// <param name="automation">The UI Automation wrapper instance.</param>
    /// <param name="cache">Optional element cache.</param>
    public ElementFactory(IUIAutomationWrapper automation, ElementCache? cache = null)
    {
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        _cache = cache;
    }

    /// <summary>
    /// Creates a UI element wrapper from a native UIA element.
    /// </summary>
    /// <param name="nativeElement">The native UIA element.</param>
    /// <returns>The wrapped UI element, or null if the native element is null.</returns>
    public IUIElement? Create(object? nativeElement)
    {
        if (nativeElement == null)
            return null;

        var element = new UIElement(nativeElement, this, _automation);

        // Check cache first
        if (_cache != null)
        {
            var cached = _cache.GetCached(element.RuntimeId);
            if (cached != null)
                return cached;

            _cache.Cache(element);
        }

        return element;
    }

    /// <summary>
    /// Gets the UI Automation wrapper instance.
    /// </summary>
    internal IUIAutomationWrapper Automation => _automation;

    /// <summary>
    /// Gets the element cache, if any.
    /// </summary>
    internal ElementCache? Cache => _cache;
}
