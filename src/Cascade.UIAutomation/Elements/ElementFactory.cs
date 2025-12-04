using Cascade.UIAutomation.Input;
using Cascade.UIAutomation.Session;
using Microsoft.Extensions.Logging;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Elements;

public sealed class ElementFactory
{
    private readonly SessionContext _context;
    private readonly IVirtualInputProvider _inputProvider;
    private readonly ILogger<ElementFactory>? _logger;

    public ElementFactory(SessionContext context, IVirtualInputProvider inputProvider, ILogger<ElementFactory>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _logger = logger;

        Cache = new ElementCache(_context.Session, RefreshInternalAsync);
    }

    public ElementCache Cache { get; }

    public IUIElement Create(AutomationElement element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        var runtimeId = element.GetRuntimeId();
        if (runtimeId is not null)
        {
            var key = string.Join(".", runtimeId);
            var cached = Cache.GetCached(key);
            if (cached is not null)
            {
                return cached;
            }
        }

        var uiElement = new UIElement(element, _context, _inputProvider, this, _logger);
        Cache.Cache(uiElement);
        return uiElement;
    }

    public IReadOnlyList<IUIElement> CreateMany(AutomationElementCollection collection)
    {
        var result = new List<IUIElement>(collection?.Count ?? 0);
        if (collection is null)
        {
            return result;
        }

        foreach (AutomationElement element in collection)
        {
            result.Add(Create(element));
        }

        return result;
    }

    internal AutomationElementCollection FindChildren(AutomationElement parent, Condition condition, TreeScope scope)
    {
        return parent.FindAll(scope, condition);
    }

    private Task<IUIElement?> RefreshInternalAsync(IUIElement element)
    {
        if (element is UIElement concrete)
        {
            return Task.FromResult<IUIElement?>(new UIElement(concrete.AutomationElement, _context, _inputProvider, this, _logger));
        }

        return Task.FromResult<IUIElement?>(null);
    }
}


