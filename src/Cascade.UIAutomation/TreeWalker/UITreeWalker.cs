using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Models;
using Microsoft.Extensions.Logging;
using System.Windows.Automation;

namespace Cascade.UIAutomation.TreeWalker;

public sealed class UITreeWalker : ITreeWalker
{
    private readonly System.Windows.Automation.TreeWalker _walker;
    private readonly ElementFactory _factory;
    private readonly ILogger<UITreeWalker>? _logger;
    private readonly Func<IUIElement, bool>? _filter;

    public UITreeWalker(
        ElementFactory factory,
        System.Windows.Automation.TreeWalker walker,
        ILogger<UITreeWalker>? logger = null,
        Func<IUIElement, bool>? filter = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _walker = walker ?? throw new ArgumentNullException(nameof(walker));
        _logger = logger;
        _filter = filter;
    }

    public ITreeWalker ControlViewWalker => new UITreeWalker(_factory, System.Windows.Automation.TreeWalker.ControlViewWalker, _logger, _filter);
    public ITreeWalker ContentViewWalker => new UITreeWalker(_factory, System.Windows.Automation.TreeWalker.ContentViewWalker, _logger, _filter);
    public ITreeWalker RawViewWalker => new UITreeWalker(_factory, System.Windows.Automation.TreeWalker.RawViewWalker, _logger, _filter);

    public ITreeWalker WithFilter(Func<IUIElement, bool> filter)
    {
        return new UITreeWalker(_factory, _walker, _logger, filter);
    }

    public IUIElement? GetParent(IUIElement element) => Wrap(_walker.GetParent(GetNative(element)));

    public IUIElement? GetFirstChild(IUIElement element) => Wrap(_walker.GetFirstChild(GetNative(element)));

    public IUIElement? GetLastChild(IUIElement element) => Wrap(_walker.GetLastChild(GetNative(element)));

    public IUIElement? GetNextSibling(IUIElement element) => Wrap(_walker.GetNextSibling(GetNative(element)));

    public IUIElement? GetPreviousSibling(IUIElement element) => Wrap(_walker.GetPreviousSibling(GetNative(element)));

    public IEnumerable<IUIElement> GetChildren(IUIElement element)
    {
        var native = GetNative(element);
        var child = _walker.GetFirstChild(native);
        while (child is not null)
        {
            var wrapped = Wrap(child);
            if (wrapped is not null)
            {
                yield return wrapped;
            }

            child = _walker.GetNextSibling(child);
        }
    }

    public IEnumerable<IUIElement> GetDescendants(IUIElement element, int maxDepth = -1)
    {
        foreach (var child in GetChildren(element))
        {
            yield return child;

            if (maxDepth == 0)
            {
                continue;
            }

            foreach (var descendant in GetDescendants(child, maxDepth < 0 ? -1 : maxDepth - 1))
            {
                yield return descendant;
            }
        }
    }

    public IEnumerable<IUIElement> GetAncestors(IUIElement element)
    {
        var parent = GetParent(element);
        while (parent is not null)
        {
            yield return parent;
            parent = GetParent(parent);
        }
    }

    public TreeSnapshot CaptureSnapshot(IUIElement root, int maxDepth = -1)
    {
        var snapshot = BuildSnapshot(root, maxDepth);
        return new TreeSnapshot
        {
            Root = snapshot.snapshot,
            CapturedAt = DateTime.UtcNow,
            TotalElements = snapshot.count
        };
    }

    private (ElementSnapshot snapshot, int count) BuildSnapshot(IUIElement element, int depthRemaining)
    {
        var childSnapshots = new List<ElementSnapshot>();
        var count = 1;

        if (depthRemaining != 0)
        {
            foreach (var child in GetChildren(element))
            {
                var (snapshot, childCount) = BuildSnapshot(child, depthRemaining < 0 ? -1 : depthRemaining - 1);
                childSnapshots.Add(snapshot);
                count += childCount;
            }
        }

        var elementSnapshot = new ElementSnapshot
        {
            RuntimeId = element.RuntimeId,
            AutomationId = element.AutomationId,
            Name = element.Name,
            ClassName = element.ClassName,
            ControlType = element.ControlType.ProgrammaticName,
            BoundingRectangle = element.BoundingRectangle,
            IsEnabled = element.IsEnabled,
            IsOffscreen = element.IsOffscreen,
            SupportedPatterns = element.SupportedPatterns.Select(p => p.ToString()).ToList(),
            Children = childSnapshots
        };

        return (elementSnapshot, count);
    }

    private AutomationElement GetNative(IUIElement element)
    {
        return (element as UIElement)?.AutomationElement
            ?? throw new InvalidOperationException("The provided element is not managed by this factory.");
    }

    private IUIElement? Wrap(AutomationElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var wrapped = _factory.Create(element);
        if (_filter is null || _filter(wrapped))
        {
            return wrapped;
        }

        return null;
    }
}


