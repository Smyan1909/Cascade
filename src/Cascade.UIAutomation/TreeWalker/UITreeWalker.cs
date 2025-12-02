using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Interop;

namespace Cascade.UIAutomation.TreeWalker;

/// <summary>
/// Implementation of ITreeWalker for navigating the UI element tree.
/// </summary>
public class UITreeWalker : ITreeWalker
{
    private readonly IUIAutomationWrapper _automation;
    private readonly ElementFactory _factory;
    private readonly object _nativeWalker;
    private readonly Func<IUIElement, bool>? _filter;
    private readonly TreeViewType _viewType;

    private UITreeWalker? _controlViewWalker;
    private UITreeWalker? _contentViewWalker;
    private UITreeWalker? _rawViewWalker;

    /// <summary>
    /// Initializes a new instance of the <see cref="UITreeWalker"/> class with control view.
    /// </summary>
    public UITreeWalker(IUIAutomationWrapper automation, ElementFactory factory)
        : this(automation, factory, TreeViewType.Control)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UITreeWalker"/> class with specified view.
    /// </summary>
    public UITreeWalker(IUIAutomationWrapper automation, ElementFactory factory, TreeViewType viewType)
    {
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _viewType = viewType;
        _nativeWalker = viewType switch
        {
            TreeViewType.Control => _automation.CreateControlViewWalker(),
            TreeViewType.Content => _automation.CreateContentViewWalker(),
            TreeViewType.Raw => _automation.CreateRawViewWalker(),
            _ => _automation.CreateControlViewWalker()
        };
    }

    /// <summary>
    /// Initializes a new instance with a custom filter.
    /// </summary>
    private UITreeWalker(IUIAutomationWrapper automation, ElementFactory factory, object nativeWalker, Func<IUIElement, bool>? filter)
    {
        _automation = automation;
        _factory = factory;
        _nativeWalker = nativeWalker;
        _filter = filter;
        _viewType = TreeViewType.Custom;
    }

    /// <inheritdoc />
    public IUIElement? GetParent(IUIElement element)
    {
        var nativeElement = GetNativeElement(element);
        var parent = _automation.GetParent(nativeElement, _nativeWalker);
        var result = _factory.Create(parent);

        if (_filter != null && result != null && !_filter(result))
            return GetParent(result); // Skip filtered elements

        return result;
    }

    /// <inheritdoc />
    public IUIElement? GetFirstChild(IUIElement element)
    {
        var nativeElement = GetNativeElement(element);
        var child = _automation.GetFirstChild(nativeElement, _nativeWalker);
        var result = _factory.Create(child);

        if (_filter != null && result != null && !_filter(result))
            return GetNextSibling(result); // Skip filtered elements

        return result;
    }

    /// <inheritdoc />
    public IUIElement? GetLastChild(IUIElement element)
    {
        var nativeElement = GetNativeElement(element);
        var child = _automation.GetLastChild(nativeElement, _nativeWalker);
        var result = _factory.Create(child);

        if (_filter != null && result != null && !_filter(result))
            return GetPreviousSibling(result); // Skip filtered elements

        return result;
    }

    /// <inheritdoc />
    public IUIElement? GetNextSibling(IUIElement element)
    {
        var nativeElement = GetNativeElement(element);
        var sibling = _automation.GetNextSibling(nativeElement, _nativeWalker);
        var result = _factory.Create(sibling);

        if (_filter != null && result != null && !_filter(result))
            return GetNextSibling(result); // Skip filtered elements

        return result;
    }

    /// <inheritdoc />
    public IUIElement? GetPreviousSibling(IUIElement element)
    {
        var nativeElement = GetNativeElement(element);
        var sibling = _automation.GetPreviousSibling(nativeElement, _nativeWalker);
        var result = _factory.Create(sibling);

        if (_filter != null && result != null && !_filter(result))
            return GetPreviousSibling(result); // Skip filtered elements

        return result;
    }

    /// <inheritdoc />
    public IEnumerable<IUIElement> GetChildren(IUIElement element)
    {
        var child = GetFirstChild(element);
        while (child != null)
        {
            yield return child;
            child = GetNextSibling(child);
        }
    }

    /// <inheritdoc />
    public IEnumerable<IUIElement> GetDescendants(IUIElement element, int maxDepth = -1)
    {
        return GetDescendantsRecursive(element, 0, maxDepth);
    }

    private IEnumerable<IUIElement> GetDescendantsRecursive(IUIElement element, int currentDepth, int maxDepth)
    {
        if (maxDepth >= 0 && currentDepth >= maxDepth)
            yield break;

        foreach (var child in GetChildren(element))
        {
            yield return child;

            foreach (var descendant in GetDescendantsRecursive(child, currentDepth + 1, maxDepth))
            {
                yield return descendant;
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<IUIElement> GetAncestors(IUIElement element)
    {
        var parent = GetParent(element);
        while (parent != null)
        {
            yield return parent;
            parent = GetParent(parent);
        }
    }

    /// <inheritdoc />
    public ITreeWalker WithFilter(Func<IUIElement, bool> filter)
    {
        return new UITreeWalker(_automation, _factory, _nativeWalker, filter);
    }

    /// <inheritdoc />
    public ITreeWalker ControlViewWalker
    {
        get
        {
            _controlViewWalker ??= new UITreeWalker(_automation, _factory, TreeViewType.Control);
            return _controlViewWalker;
        }
    }

    /// <inheritdoc />
    public ITreeWalker ContentViewWalker
    {
        get
        {
            _contentViewWalker ??= new UITreeWalker(_automation, _factory, TreeViewType.Content);
            return _contentViewWalker;
        }
    }

    /// <inheritdoc />
    public ITreeWalker RawViewWalker
    {
        get
        {
            _rawViewWalker ??= new UITreeWalker(_automation, _factory, TreeViewType.Raw);
            return _rawViewWalker;
        }
    }

    /// <inheritdoc />
    public TreeSnapshot CaptureSnapshot(IUIElement root, int maxDepth = -1)
    {
        var startTime = DateTime.UtcNow;
        int totalElements = 0;
        int actualMaxDepth = 0;

        var rootSnapshot = CaptureElementSnapshot(root, 0, maxDepth, ref totalElements, ref actualMaxDepth);

        return new TreeSnapshot(rootSnapshot, startTime, totalElements, actualMaxDepth);
    }

    private ElementSnapshot CaptureElementSnapshot(IUIElement element, int currentDepth, int maxDepth, ref int totalElements, ref int actualMaxDepth)
    {
        totalElements++;
        if (currentDepth > actualMaxDepth)
            actualMaxDepth = currentDepth;

        var snapshot = element.ToSnapshot();
        snapshot.Depth = currentDepth;

        if (maxDepth < 0 || currentDepth < maxDepth)
        {
            foreach (var child in GetChildren(element))
            {
                var childSnapshot = CaptureElementSnapshot(child, currentDepth + 1, maxDepth, ref totalElements, ref actualMaxDepth);
                snapshot.Children.Add(childSnapshot);
            }
        }

        return snapshot;
    }

    private static object GetNativeElement(IUIElement element)
    {
        if (element is UIElement uiElement)
            return uiElement.NativeElement;

        throw new ArgumentException("Element must be a UIElement instance", nameof(element));
    }
}

/// <summary>
/// Types of tree views.
/// </summary>
public enum TreeViewType
{
    /// <summary>
    /// Control view - excludes non-interactive elements.
    /// </summary>
    Control,

    /// <summary>
    /// Content view - only content-relevant elements.
    /// </summary>
    Content,

    /// <summary>
    /// Raw view - all elements.
    /// </summary>
    Raw,

    /// <summary>
    /// Custom filtered view.
    /// </summary>
    Custom
}

