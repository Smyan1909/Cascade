using Cascade.CodeGen.Generation;
using Cascade.UIAutomation.Elements;
using Cascade.Vision.Capture;

namespace Cascade.CodeGen.Execution;

public sealed class GeneratedActionExecutor : IGeneratedActionExecutor
{
    private readonly IScreenCapture? _screenCapture;
    private readonly Action<string>? _logInfo;
    private readonly Action<string>? _logWarning;

    public GeneratedActionExecutor(
        IScreenCapture? screenCapture = null,
        Action<string>? logInfo = null,
        Action<string>? logWarning = null)
    {
        _screenCapture = screenCapture;
        _logInfo = logInfo;
        _logWarning = logWarning;
    }

    public async Task ExecuteAsync(ActionRuntimeRequest action, IUIElement element, AutomationCallContext callContext, CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        var attempts = 0;
        var maxAttempts = Math.Max(1, action.RetryCount);

        if (action.CaptureScreenshotBefore && _screenCapture is not null)
        {
            await _screenCapture.CaptureElementAsync(element, cancellationToken).ConfigureAwait(false);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ExecuteInternalAsync(action, element, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception ex) when (++attempts < maxAttempts)
            {
                _logWarning?.Invoke($"Action '{action.Name}' failed on attempt {attempts}: {ex.Message}. Retrying...");
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }

        if (action.Delay is not null)
        {
            await Task.Delay(action.Delay.Value, cancellationToken).ConfigureAwait(false);
        }

        if (action.CaptureScreenshotAfter && _screenCapture is not null)
        {
            await _screenCapture.CaptureElementAsync(element, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteInternalAsync(ActionRuntimeRequest action, IUIElement element, CancellationToken cancellationToken)
    {
        switch (action.Type)
        {
            case ActionType.Click:
                await element.ClickAsync().ConfigureAwait(false);
                break;
            case ActionType.DoubleClick:
                await element.DoubleClickAsync().ConfigureAwait(false);
                break;
            case ActionType.RightClick:
                await element.RightClickAsync().ConfigureAwait(false);
                break;
            case ActionType.Type:
                var text = action.Parameters.TryGetValue("text", out var value)
                    ? value?.ToString() ?? string.Empty
                    : string.Empty;
                await element.TypeTextAsync(text).ConfigureAwait(false);
                break;
            case ActionType.SetValue:
                var setter = action.Parameters.TryGetValue("value", out var setValue)
                    ? setValue?.ToString() ?? string.Empty
                    : string.Empty;
                await element.SetValueAsync(setter).ConfigureAwait(false);
                break;
            case ActionType.Invoke:
                await element.InvokeAsync().ConfigureAwait(false);
                break;
            case ActionType.WaitForElement:
                await Task.Delay(TimeSpan.FromMilliseconds(
                    action.Parameters.TryGetValue("delayMs", out var delay)
                        ? Convert.ToDouble(delay, System.Globalization.CultureInfo.InvariantCulture)
                        : 500), cancellationToken).ConfigureAwait(false);
                break;
            case ActionType.Custom:
                _logInfo?.Invoke($"Custom action '{action.Name}' executed with parameters: {string.Join(", ", action.Parameters.Select(p => $"{p.Key}={p.Value}"))}");
                break;
            default:
                throw new NotSupportedException($"Action type '{action.Type}' is not supported.");
        }
    }
}

