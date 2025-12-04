namespace Cascade.UIAutomation.Patterns;

public interface IValuePattern : IPatternProvider<System.Windows.Automation.ValuePattern>
{
    string Value { get; }
    bool IsReadOnly { get; }
    Task SetValueAsync(string value);
}


