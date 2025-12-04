using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Models;

namespace Cascade.UIAutomation.TreeWalker;

public sealed class FilteredTreeWalker : ITreeWalker
{
    private readonly ITreeWalker _inner;

    public FilteredTreeWalker(ITreeWalker inner, Func<IUIElement, bool> filter)
    {
        _inner = inner.WithFilter(filter);
    }

    public ITreeWalker ControlViewWalker => _inner.ControlViewWalker;
    public ITreeWalker ContentViewWalker => _inner.ContentViewWalker;
    public ITreeWalker RawViewWalker => _inner.RawViewWalker;

    public TreeSnapshot CaptureSnapshot(IUIElement root, int maxDepth = -1) => _inner.CaptureSnapshot(root, maxDepth);
    public IEnumerable<IUIElement> GetAncestors(IUIElement element) => _inner.GetAncestors(element);
    public IEnumerable<IUIElement> GetChildren(IUIElement element) => _inner.GetChildren(element);
    public IEnumerable<IUIElement> GetDescendants(IUIElement element, int maxDepth = -1) => _inner.GetDescendants(element, maxDepth);
    public IUIElement? GetFirstChild(IUIElement element) => _inner.GetFirstChild(element);
    public IUIElement? GetLastChild(IUIElement element) => _inner.GetLastChild(element);
    public IUIElement? GetNextSibling(IUIElement element) => _inner.GetNextSibling(element);
    public IUIElement? GetParent(IUIElement element) => _inner.GetParent(element);
    public IUIElement? GetPreviousSibling(IUIElement element) => _inner.GetPreviousSibling(element);
    public ITreeWalker WithFilter(Func<IUIElement, bool> filter) => _inner.WithFilter(filter);
}


