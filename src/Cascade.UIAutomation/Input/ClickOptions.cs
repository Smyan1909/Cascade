namespace Cascade.UIAutomation.Input;

public sealed class ClickOptions
{
    public int DelayBeforeMs { get; init; } = 10;
    public int DelayAfterMs { get; init; } = 10;
    public int ClickCount { get; init; } = 1;
    public bool EnsureFocus { get; init; } = true;
}


