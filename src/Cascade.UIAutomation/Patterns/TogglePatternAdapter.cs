using System.Windows.Automation;

namespace Cascade.UIAutomation.Patterns;

internal sealed class TogglePatternAdapter : ITogglePattern
{
    public TogglePatternAdapter(TogglePattern nativePattern)
    {
        NativePattern = nativePattern ?? throw new ArgumentNullException(nameof(nativePattern));
    }

    public TogglePattern NativePattern { get; }

    public ToggleState ToggleState => NativePattern.Current.ToggleState;

    public Task ToggleAsync()
    {
        NativePattern.Toggle();
        return Task.CompletedTask;
    }
}


