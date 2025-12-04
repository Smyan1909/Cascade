using System.Windows.Automation;

namespace Cascade.UIAutomation.Patterns;

internal sealed class ExpandCollapsePatternAdapter : IExpandCollapsePattern
{
    public ExpandCollapsePatternAdapter(ExpandCollapsePattern nativePattern)
    {
        NativePattern = nativePattern ?? throw new ArgumentNullException(nameof(nativePattern));
    }

    public ExpandCollapsePattern NativePattern { get; }

    public ExpandCollapseState State => NativePattern.Current.ExpandCollapseState;

    public Task ExpandAsync()
    {
        NativePattern.Expand();
        return Task.CompletedTask;
    }

    public Task CollapseAsync()
    {
        NativePattern.Collapse();
        return Task.CompletedTask;
    }
}


