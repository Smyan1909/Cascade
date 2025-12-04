using Microsoft.Extensions.Logging;

namespace Cascade.UIAutomation.Input;

public sealed class VirtualKeyboard
{
    private readonly ILogger<VirtualKeyboard>? _logger;

    public VirtualKeyboard(ILogger<VirtualKeyboard>? logger = null)
    {
        _logger = logger;
    }

    public Task TypeTextAsync(string text, TextEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new TextEntryOptions();
        if (options.ClearBeforeTyping)
        {
            _logger?.LogDebug("Clearing text before typing '{Text}'", text);
        }

        return Task.Delay(Math.Max(1, text.Length * options.DelayBetweenCharactersMs), cancellationToken);
    }

    public Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("Send virtual key {Key}", key);
        return Task.CompletedTask;
    }
}


