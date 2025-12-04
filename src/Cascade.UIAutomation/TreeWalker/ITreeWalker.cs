using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Models;

namespace Cascade.UIAutomation.TreeWalker;

public interface ITreeWalker
{
    IUIElement? GetParent(IUIElement element);
    IUIElement? GetFirstChild(IUIElement element);
    IUIElement? GetLastChild(IUIElement element);
    IUIElement? GetNextSibling(IUIElement element);
    IUIElement? GetPreviousSibling(IUIElement element);

    IEnumerable<IUIElement> GetChildren(IUIElement element);
    IEnumerable<IUIElement> GetDescendants(IUIElement element, int maxDepth = -1);
    IEnumerable<IUIElement> GetAncestors(IUIElement element);

    ITreeWalker WithFilter(Func<IUIElement, bool> filter);
    ITreeWalker ControlViewWalker { get; }
    ITreeWalker ContentViewWalker { get; }
    ITreeWalker RawViewWalker { get; }

    TreeSnapshot CaptureSnapshot(IUIElement root, int maxDepth = -1);
}


