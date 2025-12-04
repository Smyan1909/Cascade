namespace Cascade.UIAutomation.Patterns;

public interface ITogglePattern : IPatternProvider<System.Windows.Automation.TogglePattern>
{
    System.Windows.Automation.ToggleState ToggleState { get; }
    Task ToggleAsync();
}


