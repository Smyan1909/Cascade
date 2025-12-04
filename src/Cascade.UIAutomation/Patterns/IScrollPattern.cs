namespace Cascade.UIAutomation.Patterns;

public interface IScrollPattern : IPatternProvider<System.Windows.Automation.ScrollPattern>
{
    double HorizontalScrollPercent { get; }
    double VerticalScrollPercent { get; }
    double HorizontalViewSize { get; }
    double VerticalViewSize { get; }
    bool HorizontallyScrollable { get; }
    bool VerticallyScrollable { get; }

    Task ScrollAsync(System.Windows.Automation.ScrollAmount horizontal, System.Windows.Automation.ScrollAmount vertical);
    Task SetScrollPercentAsync(double horizontal, double vertical);
}


