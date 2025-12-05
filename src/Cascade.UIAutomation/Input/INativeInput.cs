using System.Drawing;

namespace Cascade.UIAutomation.Input;

/// <summary>
/// Abstraction over native input so it can be faked in tests.
/// </summary>
public interface INativeInput
{
    Task SendUnicodeCharAsync(char character, CancellationToken cancellationToken = default);
    Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null, CancellationToken cancellationToken = default);
    Task MoveMouseAsync(Point screenPoint, CancellationToken cancellationToken = default);
    Task ClickAsync(MouseButton button, ClickOptions? options = null, CancellationToken cancellationToken = default);
    Task ScrollAsync(int delta, ScrollOptions? options = null, CancellationToken cancellationToken = default);
}


