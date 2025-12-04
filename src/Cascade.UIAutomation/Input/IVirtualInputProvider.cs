using Cascade.UIAutomation.Session;
using System.Drawing;

namespace Cascade.UIAutomation.Input;

public interface IVirtualInputProvider
{
    SessionHandle Session { get; }

    Task MoveMouseAsync(Point screenPoint, CancellationToken cancellationToken = default);
    Task ClickAsync(MouseButton button, ClickOptions? options = null, CancellationToken cancellationToken = default);
    Task TypeTextAsync(string text, TextEntryOptions? options = null, CancellationToken cancellationToken = default);
    Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null, CancellationToken cancellationToken = default);
    Task ScrollAsync(int delta, ScrollOptions? options = null, CancellationToken cancellationToken = default);
}


