namespace Cascade.UIAutomation.Patterns;

public interface IInvokePattern : IPatternProvider<System.Windows.Automation.InvokePattern>
{
    Task InvokeAsync();
}


