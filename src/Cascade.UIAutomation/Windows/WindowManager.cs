using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Windows;

public sealed class WindowManager : IWindowManager
{
    private readonly IElementDiscovery _discovery;
    private readonly ILogger<WindowManager>? _logger;

    public WindowManager(IElementDiscovery discovery, ILogger<WindowManager>? logger = null)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger;
    }

    public Task<bool> SetForegroundAsync(IUIElement window)
    {
        try
        {
            var element = GetNative(window);
            if (element.TryGetCurrentPattern(WindowPattern.Pattern, out var patternObj) && patternObj is WindowPattern pattern)
            {
                pattern.SetWindowVisualState(WindowVisualState.Normal);
                pattern.WaitForInputIdle(500);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set foreground window.");
        }

        return Task.FromResult(false);
    }

    public Task MinimizeAsync(IUIElement window) => SetState(window, WindowVisualState.Minimized);
    public Task MaximizeAsync(IUIElement window) => SetState(window, WindowVisualState.Maximized);
    public Task RestoreAsync(IUIElement window) => SetState(window, WindowVisualState.Normal);

    public Task CloseAsync(IUIElement window)
    {
        if (GetNative(window).TryGetCurrentPattern(WindowPattern.Pattern, out var patternObj) && patternObj is WindowPattern pattern)
        {
            pattern.Close();
        }

        return Task.CompletedTask;
    }

    public Task MoveAsync(IUIElement window, int x, int y)
    {
        var transform = GetTransformPattern(window);
        transform.Move(x, y);
        return Task.CompletedTask;
    }

    public Task ResizeAsync(IUIElement window, int width, int height)
    {
        var transform = GetTransformPattern(window);
        transform.Resize(width, height);
        return Task.CompletedTask;
    }

    public IUIElement? AttachToProcess(int processId)
    {
        return _discovery.GetAllWindows()
            .FirstOrDefault(window => TryGetProcessId(window, out var pid) && pid == processId);
    }

    public IUIElement? AttachToProcess(string processName)
    {
        var process = Process.GetProcessesByName(processName).FirstOrDefault();
        return process is null ? null : AttachToProcess(process.Id);
    }

    public IUIElement? LaunchAndAttach(string executablePath, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo(executablePath, arguments ?? string.Empty)
        {
            UseShellExecute = true
        };

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        process.WaitForInputIdle();
        return AttachToProcess(process.Id);
    }

    private static Task SetState(IUIElement window, WindowVisualState state)
    {
        var native = GetNative(window);
        if (native.TryGetCurrentPattern(WindowPattern.Pattern, out var patternObj) && patternObj is WindowPattern pattern)
        {
            pattern.SetWindowVisualState(state);
        }

        return Task.CompletedTask;
    }

    private static AutomationElement GetNative(IUIElement element)
    {
        return (element as UIElement)?.AutomationElement
            ?? throw new InvalidOperationException("Element is not managed by UIAutomation.");
    }

    private static TransformPattern GetTransformPattern(IUIElement element)
    {
        var native = GetNative(element);
        if (!native.TryGetCurrentPattern(TransformPattern.Pattern, out var pattern) || pattern is not TransformPattern transform)
        {
            throw new InvalidOperationException("Element does not support TransformPattern.");
        }

        return transform;
    }

    private static bool TryGetProcessId(IUIElement element, out int processId)
    {
        var native = GetNative(element);
        processId = native.Current.ProcessId;
        return processId > 0;
    }
}


