using System.Windows.Automation;

namespace Cascade.UIAutomation.Patterns;

internal sealed class ValuePatternAdapter : IValuePattern
{
    public ValuePatternAdapter(ValuePattern nativePattern)
    {
        NativePattern = nativePattern ?? throw new ArgumentNullException(nameof(nativePattern));
    }

    public ValuePattern NativePattern { get; }

    public string Value => NativePattern.Current.Value;
    public bool IsReadOnly => NativePattern.Current.IsReadOnly;

    public Task SetValueAsync(string value)
    {
        NativePattern.SetValue(value);
        return Task.CompletedTask;
    }
}


