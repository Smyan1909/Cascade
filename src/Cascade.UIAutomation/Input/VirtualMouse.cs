using Cascade.UIAutomation.Session;
using Microsoft.Extensions.Logging;
using System.Drawing;

namespace Cascade.UIAutomation.Input;

/// <summary>
/// Default virtual HID provider that routes events through the session host.
/// </summary>
public sealed class VirtualMouse : IVirtualInputProvider
{
    private readonly VirtualKeyboard _keyboard;
    private readonly INativeInput _nativeInput;
    private readonly ILogger<VirtualMouse>? _logger;

    public VirtualMouse(SessionHandle session, VirtualKeyboard keyboard, INativeInput? nativeInput = null, ILogger<VirtualMouse>? logger = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        _nativeInput = nativeInput ?? new NativeInput();
        _logger = logger;
    }

    public SessionHandle Session { get; }

    public Task ClickAsync(MouseButton button, ClickOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ClickOptions();
        _logger?.LogDebug("Click {Button} ({Clicks}x) on session {SessionId}", button, options.ClickCount, Session.SessionId);
        return _nativeInput.ClickAsync(button, options, cancellationToken);
    }

    public Task MoveMouseAsync(Point screenPoint, CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("Move cursor to {Point} in session {SessionId}", screenPoint, Session.SessionId);
        return _nativeInput.MoveMouseAsync(screenPoint, cancellationToken);
    }

    public Task ScrollAsync(int delta, ScrollOptions? options = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("Scroll delta {Delta} (horizontal={Horizontal})", delta, options?.Horizontal ?? false);
        return _nativeInput.ScrollAsync(delta, options, cancellationToken);
    }

    public Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null, CancellationToken cancellationToken = default)
    {
        return _keyboard.SendVirtualKeyAsync(key, options, cancellationToken);
    }

    public Task TypeTextAsync(string text, TextEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        return _keyboard.TypeTextAsync(text, options, cancellationToken);
    }
}


