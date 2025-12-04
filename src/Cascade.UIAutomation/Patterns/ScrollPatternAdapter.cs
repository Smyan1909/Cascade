using System.Windows.Automation;

namespace Cascade.UIAutomation.Patterns;

internal sealed class ScrollPatternAdapter : IScrollPattern
{
    public ScrollPatternAdapter(ScrollPattern nativePattern)
    {
        NativePattern = nativePattern ?? throw new ArgumentNullException(nameof(nativePattern));
    }

    public ScrollPattern NativePattern { get; }

    public double HorizontalScrollPercent => NativePattern.Current.HorizontalScrollPercent;
    public double VerticalScrollPercent => NativePattern.Current.VerticalScrollPercent;
    public double HorizontalViewSize => NativePattern.Current.HorizontalViewSize;
    public double VerticalViewSize => NativePattern.Current.VerticalViewSize;
    public bool HorizontallyScrollable => NativePattern.Current.HorizontallyScrollable;
    public bool VerticallyScrollable => NativePattern.Current.VerticallyScrollable;

    public Task ScrollAsync(ScrollAmount horizontal, ScrollAmount vertical)
    {
        NativePattern.Scroll(horizontal, vertical);
        return Task.CompletedTask;
    }

    public Task SetScrollPercentAsync(double horizontal, double vertical)
    {
        NativePattern.SetScrollPercent(horizontal, vertical);
        return Task.CompletedTask;
    }
}


