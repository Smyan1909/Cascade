namespace Cascade.UIAutomation.Patterns;

public interface IPatternProvider<out TNative>
{
    TNative NativePattern { get; }
}


