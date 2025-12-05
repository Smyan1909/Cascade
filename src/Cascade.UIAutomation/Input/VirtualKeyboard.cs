using Microsoft.Extensions.Logging;

namespace Cascade.UIAutomation.Input;

public sealed class VirtualKeyboard
{
    private readonly INativeInput _nativeInput;
    private readonly ILogger<VirtualKeyboard>? _logger;

    public VirtualKeyboard(INativeInput? nativeInput = null, ILogger<VirtualKeyboard>? logger = null)
    {
        _nativeInput = nativeInput ?? new NativeInput();
        _logger = logger;
    }

    public async Task TypeTextAsync(string text, TextEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new TextEntryOptions();
        cancellationToken.ThrowIfCancellationRequested();

        if (options.ClearBeforeTyping)
        {
            _logger?.LogDebug("Clearing text before typing '{Text}'", text);
            await SendChordAsync(VirtualKey.Control, VirtualKey.A, cancellationToken).ConfigureAwait(false);
            await SendVirtualKeyAsync(VirtualKey.Backspace, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        foreach (var ch in text ?? string.Empty)
        {
            await _nativeInput.SendUnicodeCharAsync(ch, cancellationToken).ConfigureAwait(false);
            if (options.DelayBetweenCharactersMs > 0)
            {
                await Task.Delay(options.DelayBetweenCharactersMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("Send virtual key {Key}", key);
        return _nativeInput.SendVirtualKeyAsync(key, options, cancellationToken);
    }

    private async Task SendChordAsync(VirtualKey modifier, VirtualKey key, CancellationToken cancellationToken)
    {
        await _nativeInput.SendVirtualKeyAsync(modifier, new KeySendOptions { KeyDown = true, KeyUp = false }, cancellationToken).ConfigureAwait(false);
        await _nativeInput.SendVirtualKeyAsync(key, new KeySendOptions { KeyDown = true, KeyUp = true }, cancellationToken).ConfigureAwait(false);
        await _nativeInput.SendVirtualKeyAsync(modifier, new KeySendOptions { KeyDown = false, KeyUp = true }, cancellationToken).ConfigureAwait(false);
    }
}


