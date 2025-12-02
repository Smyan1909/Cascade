using System.Diagnostics;
using System.Runtime.InteropServices;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.Exceptions;
using Cascade.UIAutomation.Patterns;
using FlaUI.Core.AutomationElements;

namespace Cascade.UIAutomation.Windows;

/// <summary>
/// Implementation of IWindowManager for window management operations.
/// </summary>
public class WindowManager : IWindowManager
{
    private readonly IElementDiscovery _discovery;

    #region P/Invoke Declarations

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowManager"/> class.
    /// </summary>
    public WindowManager(IElementDiscovery discovery)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    /// <inheritdoc />
    public Task<bool> SetForegroundAsync(IUIElement window)
    {
        return Task.Run(() =>
        {
            var hwnd = GetWindowHandle(window);
            if (hwnd == IntPtr.Zero)
                return false;

            // Get the foreground window's thread
            var foregroundHwnd = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundHwnd, out _);
            var foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
            var currentThreadId = GetCurrentThreadId();

            // Attach input threads
            if (foregroundThreadId != currentThreadId)
            {
                AttachThreadInput(foregroundThreadId, currentThreadId, true);
            }

            try
            {
                BringWindowToTop(hwnd);
                ShowWindow(hwnd, SW_SHOW);
                return SetForegroundWindow(hwnd);
            }
            finally
            {
                if (foregroundThreadId != currentThreadId)
                {
                    AttachThreadInput(foregroundThreadId, currentThreadId, false);
                }
            }
        });
    }

    /// <inheritdoc />
    public Task MinimizeAsync(IUIElement window)
    {
        return Task.Run(() =>
        {
            // Try using WindowPattern first
            if (window.TryGetPattern<IWindowPattern>(out var pattern) && pattern != null)
            {
                return pattern.SetWindowVisualStateAsync(WindowVisualState.Minimized);
            }

            // Fall back to Win32 API
            var hwnd = GetWindowHandle(window);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_MINIMIZE);
            }

            return Task.CompletedTask;
        });
    }

    /// <inheritdoc />
    public Task MaximizeAsync(IUIElement window)
    {
        return Task.Run(() =>
        {
            // Try using WindowPattern first
            if (window.TryGetPattern<IWindowPattern>(out var pattern) && pattern != null)
            {
                return pattern.SetWindowVisualStateAsync(WindowVisualState.Maximized);
            }

            // Fall back to Win32 API
            var hwnd = GetWindowHandle(window);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_MAXIMIZE);
            }

            return Task.CompletedTask;
        });
    }

    /// <inheritdoc />
    public Task RestoreAsync(IUIElement window)
    {
        return Task.Run(() =>
        {
            // Try using WindowPattern first
            if (window.TryGetPattern<IWindowPattern>(out var pattern) && pattern != null)
            {
                return pattern.SetWindowVisualStateAsync(WindowVisualState.Normal);
            }

            // Fall back to Win32 API
            var hwnd = GetWindowHandle(window);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            return Task.CompletedTask;
        });
    }

    /// <inheritdoc />
    public Task CloseAsync(IUIElement window)
    {
        return Task.Run(() =>
        {
            // Try using WindowPattern first
            if (window.TryGetPattern<IWindowPattern>(out var pattern) && pattern != null)
            {
                return pattern.CloseAsync();
            }

            throw UIAutomationException.PatternNotSupported(nameof(IWindowPattern), window.RuntimeId);
        });
    }

    /// <inheritdoc />
    public Task MoveAsync(IUIElement window, int x, int y)
    {
        return Task.Run(() =>
        {
            // Try using TransformPattern first
            if (window.TryGetPattern<ITransformPattern>(out var pattern) && pattern != null && pattern.CanMove)
            {
                return pattern.MoveAsync(x, y);
            }

            // Fall back to Win32 API
            var hwnd = GetWindowHandle(window);
            if (hwnd != IntPtr.Zero)
            {
                GetWindowRect(hwnd, out var rect);
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                MoveWindow(hwnd, x, y, width, height, true);
            }

            return Task.CompletedTask;
        });
    }

    /// <inheritdoc />
    public Task ResizeAsync(IUIElement window, int width, int height)
    {
        return Task.Run(() =>
        {
            // Try using TransformPattern first
            if (window.TryGetPattern<ITransformPattern>(out var pattern) && pattern != null && pattern.CanResize)
            {
                return pattern.ResizeAsync(width, height);
            }

            // Fall back to Win32 API
            var hwnd = GetWindowHandle(window);
            if (hwnd != IntPtr.Zero)
            {
                GetWindowRect(hwnd, out var rect);
                MoveWindow(hwnd, rect.Left, rect.Top, width, height, true);
            }

            return Task.CompletedTask;
        });
    }

    /// <inheritdoc />
    public IUIElement? AttachToProcess(int processId)
    {
        return _discovery.GetMainWindow(processId);
    }

    /// <inheritdoc />
    public IUIElement? AttachToProcess(string processName)
    {
        return _discovery.GetMainWindow(processName);
    }

    /// <inheritdoc />
    public async Task<IUIElement?> LaunchAndAttachAsync(string executablePath, string? arguments = null, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true
        };

        var process = Process.Start(startInfo);
        if (process == null)
            throw UIAutomationException.ProcessNotFound(executablePath);

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    var window = _discovery.ElementFromHandle(process.MainWindowHandle);
                    if (window != null)
                        return window;
                }

                // Also try finding by process ID
                var windowByPid = _discovery.GetMainWindow(process.Id);
                if (windowByPid != null)
                    return windowByPid;
            }
            catch
            {
                // Process may not be ready yet
            }

            await Task.Delay(100);
        }

        throw UIAutomationException.Timeout("LaunchAndAttach", timeout.Value);
    }

    /// <inheritdoc />
    public async Task<bool> WaitForInputIdleAsync(IUIElement window, TimeSpan timeout)
    {
        if (window.TryGetPattern<IWindowPattern>(out var pattern) && pattern != null)
        {
            return await pattern.WaitForInputIdleAsync((int)timeout.TotalMilliseconds);
        }

        // Fall back to simple delay
        await Task.Delay(timeout);
        return true;
    }

    private IntPtr GetWindowHandle(IUIElement window)
    {
        // Try to get the native window handle property
        if (window is UIElement uiElement)
        {
            try
            {
                var nativeElement = uiElement.NativeElement;
                return nativeElement.Properties.NativeWindowHandle.ValueOrDefault;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }
}
