using System.Windows.Automation;

namespace Cascade.UIAutomation.Patterns;

internal sealed class InvokePatternAdapter : IInvokePattern
{
    public InvokePatternAdapter(InvokePattern nativePattern)
    {
        NativePattern = nativePattern ?? throw new ArgumentNullException(nameof(nativePattern));
    }

    public InvokePattern NativePattern { get; }

    public Task InvokeAsync()
    {
        NativePattern.Invoke();
        return Task.CompletedTask;
    }
}


