using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Windows;

public interface IWindowManager
{
    Task<bool> SetForegroundAsync(IUIElement window);
    Task MinimizeAsync(IUIElement window);
    Task MaximizeAsync(IUIElement window);
    Task RestoreAsync(IUIElement window);
    Task CloseAsync(IUIElement window);

    Task MoveAsync(IUIElement window, int x, int y);
    Task ResizeAsync(IUIElement window, int width, int height);

    IUIElement? AttachToProcess(int processId);
    IUIElement? AttachToProcess(string processName);
    IUIElement? LaunchAndAttach(string executablePath, string? arguments = null);
}


