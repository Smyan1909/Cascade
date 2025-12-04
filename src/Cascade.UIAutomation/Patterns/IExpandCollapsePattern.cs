namespace Cascade.UIAutomation.Patterns;

public interface IExpandCollapsePattern : IPatternProvider<System.Windows.Automation.ExpandCollapsePattern>
{
    System.Windows.Automation.ExpandCollapseState State { get; }
    Task ExpandAsync();
    Task CollapseAsync();
}


