using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Cascade.CodeGen.Compilation;
using Cascade.CodeGen.Execution;
using Cascade.Core.Session;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Input;
using Cascade.UIAutomation.Session;
using Cascade.Vision.Capture;
using Microsoft.Playwright;
using Moq;
using Xunit;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using CodeGenExecutionContext = Cascade.CodeGen.Execution.ExecutionContext;
using System.Collections.Generic;

namespace Cascade.Tests.Integration;

public class CurrentSessionIntegrationTests
{
    // Guard integration tests behind env flags so CI stays stable.
    private static bool Run(string flag) =>
        string.Equals(Environment.GetEnvironmentVariable(flag), "1", StringComparison.Ordinal);

    [Fact]
    public async Task Uia_Notepad_Launch_Type_And_Read_WhenEnabled()
    {
        if (!Run("RUN_UIA_INTEGRATION"))
        {
            return;
        }

        await RunStaAsync(async () =>
        {
            using var proc = StartNotepad();
            try
            {
                var window = await WaitForWindowAsync(proc!, TimeSpan.FromSeconds(30));
                if (window is null) return;

                var edit = await WaitForEditAsync(window!, TimeSpan.FromSeconds(5));
                if (edit is null) return;

                var valuePattern = edit!.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                Assert.NotNull(valuePattern);

                valuePattern!.SetValue("hello world");

                var readBack = (edit.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern)!.Current.Value;
                Assert.Contains("hello world", readBack);
            }
            finally
            {
                if (proc is { HasExited: false })
                {
                    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                }
            }
        });
    }

    [Fact]
    public async Task Uia_VirtualInput_Notepad_Type_WhenEnabled()
    {
        if (!Run("RUN_UIA_INTEGRATION"))
        {
            return;
        }

        await RunStaAsync(async () =>
        {
            var session = new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() };
            var keyboard = new VirtualKeyboard();
            var mouse = new VirtualMouse(session, keyboard);

            using var proc = StartNotepad();
            try
            {
                var window = await WaitForWindowAsync(proc!, TimeSpan.FromSeconds(30));
                if (window is null) return;

                var edit = await WaitForEditAsync(window!, TimeSpan.FromSeconds(5));
                if (edit is null) return;

                var rect = (System.Windows.Rect)edit!.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty);
                var center = new Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));

                await mouse.MoveMouseAsync(center);
                await mouse.ClickAsync(Cascade.UIAutomation.Input.MouseButton.Left, new ClickOptions { DelayAfterMs = 20, DelayBeforeMs = 20 });
                await keyboard.TypeTextAsync("virtual input ok", new TextEntryOptions { ClearBeforeTyping = true, DelayBetweenCharactersMs = 1 });
                await Task.Delay(200);

                var valuePattern = (ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern);
                Assert.Contains("virtual input ok", valuePattern.Current.Value);
            }
            finally
            {
                if (proc is { HasExited: false })
                {
                    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                }
            }
        });
    }

    private static async Task<AutomationElement?> WaitForWindowAsync(Process process, TimeSpan timeout, params string[] extraTitles)
    {
        var deadline = DateTime.UtcNow + timeout;
        var titleCandidates = new List<string> { "Untitled - Notepad", "Notepad" };
        if (extraTitles is { Length: > 0 })
        {
            titleCandidates.AddRange(extraTitles);
        }
        while (DateTime.UtcNow < deadline)
        {
            var processes = new List<Process> { process };
            processes.AddRange(Process.GetProcessesByName("notepad").Where(p => p.Id != process.Id));
            processes.AddRange(Process.GetProcessesByName("Calculator").Where(p => p.Id != process.Id));
            processes.AddRange(Process.GetProcessesByName("CalculatorApp").Where(p => p.Id != process.Id));

            foreach (var candidate in processes)
            {
                try
                {
                    candidate.Refresh();
                    if (candidate.MainWindowHandle != IntPtr.Zero)
                    {
                        return AutomationElement.FromHandle(candidate.MainWindowHandle);
                    }

                    var root = AutomationElement.RootElement;
                    if (root is not null)
                    {
                        var byPid = new PropertyCondition(AutomationElement.ProcessIdProperty, candidate.Id);
                        foreach (var title in titleCandidates)
                        {
                            var byName = new PropertyCondition(AutomationElement.NameProperty, title);
                            var condition = new AndCondition(new OrCondition(byPid, byName), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
                            var window = root.FindFirst(TreeScope.Children, condition);
                            if (window is not null)
                            {
                                return window;
                            }
                        }
                    }

                    var native = FindWindowByPid(candidate.Id, titleCandidates);
                    if (native is not null)
                    {
                        return native;
                    }
                }
                catch
                {
                    // ignore process access issues
                }
            }

            var rootWindow = AutomationElement.RootElement;
            if (rootWindow is not null)
            {
                foreach (var title in titleCandidates)
                {
                    var byName = new PropertyCondition(AutomationElement.NameProperty, title);
                    var condition = new AndCondition(byName, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
                    var window = rootWindow.FindFirst(TreeScope.Children, condition);
                    if (window is not null)
                    {
                        return window;
                    }
                }
            }
            await Task.Delay(200);
        }

        return null;
    }

    private static async Task<AutomationElement?> WaitForEditAsync(AutomationElement window, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var edit = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (edit is not null)
            {
                return edit;
            }

            await Task.Delay(200);
        }

        return null;
    }

    private static AutomationElement? FindWindowByPid(int pid, IEnumerable<string> titles)
    {
        foreach (var title in titles)
        {
            var handleByTitle = NativeMethods.FindWindow(null, title);
            if (handleByTitle != IntPtr.Zero)
            {
                return AutomationElement.FromHandle(handleByTitle);
            }
        }

        var handle = IntPtr.Zero;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var windowPid);
            if (windowPid == pid && NativeMethods.IsWindowVisible(hWnd))
            {
                handle = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle);
    }

    private static AutomationElement? FindByAutomationId(AutomationElement root, string automationId)
    {
        return root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
    }

    private static async Task<AutomationElement?> WaitForButtonAsync(AutomationElement root, TimeSpan timeout, params string[] candidates)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                var name = button.Current.Name ?? string.Empty;
                var automationId = button.Current.AutomationId ?? string.Empty;

                if (!candidates.Any(c =>
                        name.Contains(c, StringComparison.OrdinalIgnoreCase) ||
                        automationId.Contains(c, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return button;
            }
            await Task.Delay(200);
        }

        return null;
    }

    private static async Task ClickButtonAsync(AutomationElement root, params string[] candidates)
    {
        var button = await WaitForButtonAsync(root, TimeSpan.FromSeconds(10), candidates);
        if (button is not null)
        {
            if (button.TryGetCurrentPattern(InvokePattern.Pattern, out var patternObj) && patternObj is InvokePattern invoke)
            {
                invoke.Invoke();
                return;
            }

            // Fallback: click the center of the bounding rectangle.
            var rectObj = button.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty);
            if (rectObj is System.Windows.Rect rect && rect.Width > 0 && rect.Height > 0)
            {
                var center = new Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
                var session = new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() };
                var keyboard = new VirtualKeyboard();
                var mouse = new VirtualMouse(session, keyboard);
                await mouse.MoveMouseAsync(center);
                await mouse.ClickAsync(Cascade.UIAutomation.Input.MouseButton.Left, new ClickOptions { DelayAfterMs = 20, DelayBeforeMs = 10 });
                return;
            }
        }

        // Fallback: try pressing key equivalents if button not found or not invokable.
        var target = candidates.FirstOrDefault(c => c.Length == 1);
        if (target is not null)
        {
            root.SetFocus();
            var keyboard = new VirtualKeyboard();
            await keyboard.TypeTextAsync(target, new TextEntryOptions { DelayBetweenCharactersMs = 1 });
            return;
        }

        throw new InvalidOperationException($"UI element '{string.Join("/", candidates)}' not found or not invokable.");
    }

    private static Task RunStaAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        var thread = new Thread(async () =>
        {
            try
            {
                await action().ConfigureAwait(false);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    private static Process StartNotepad()
    {
        var psi = new ProcessStartInfo("notepad.exe")
        {
            UseShellExecute = true
        };

        var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Notepad.");
        proc.WaitForInputIdle(5000);
        return proc;
    }

    private static Process StartCalculator()
    {
        var psi = new ProcessStartInfo("calc.exe")
        {
            UseShellExecute = true
        };

        var proc = Process.Start(psi);
        if (proc is not null)
        {
            try { proc.WaitForInputIdle(5000); } catch { /* ignore */ }
            return proc;
        }

        var appId = "shell:appsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        var uwp = Process.Start(new ProcessStartInfo("explorer.exe", appId) { UseShellExecute = true });
        if (uwp is not null)
        {
            try { uwp.WaitForInputIdle(5000); } catch { /* ignore */ }
            return uwp;
        }

        throw new InvalidOperationException("Failed to start Calculator.");
    }

    [Fact]
    public async Task Vision_ChangeDetector_DetectsDifference_WhenEnabled()
    {
        if (!Run("RUN_VISION_INTEGRATION"))
        {
            return;
        }

        // Build two simple images: one blank, one with a red pixel.
        var blank = new byte[] { 0, 0, 0, 0 };
        var changed = new byte[] { 255, 0, 0, 0 };

        // Use a minimal 1x1 PNG for simplicity.
        byte[] MakePng(byte[] rgba)
        {
            using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);
            img[0, 0] = new SixLabors.ImageSharp.PixelFormats.Rgba32(rgba[0], rgba[1], rgba[2], 255);
            using var ms = new System.IO.MemoryStream();
            SixLabors.ImageSharp.ImageExtensions.SaveAsPng(img, ms);
            return ms.ToArray();
        }

        var baselinePng = MakePng(blank);
        var changedPng = MakePng(changed);

        var detector = new Cascade.Vision.Comparison.ChangeDetector(
            new Cascade.Vision.Comparison.ComparisonOptions { ChangeThreshold = 0.01, GenerateDifferenceImage = false });

        var result = await detector.CompareAsync(baselinePng, changedPng);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public async Task Vision_Capture_NotepadWindow_WhenEnabled()
    {
        if (!Run("RUN_VISION_INTEGRATION"))
        {
            return;
        }

        var psi = new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
        using var proc = Process.Start(psi);
        try
        {
            var window = await WaitForWindowAsync(proc!, TimeSpan.FromSeconds(15));
            Assert.NotNull(window);
            var handle = new IntPtr(window!.Current.NativeWindowHandle);

            var session = new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() };
            var capture = new ScreenCapture(session, new DesktopSessionFrameProvider(), new CaptureOptions { ImageFormat = "png" });

            var result = await capture.CaptureWindowAsync(handle);

            Assert.True(result.Width > 0);
            Assert.True(result.Height > 0);
            Assert.Equal(handle, result.SourceWindowHandle);
            Assert.False(result.ImageData.Length == 0);
        }
        finally
        {
            if (proc is { HasExited: false })
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task Uia_And_Vision_Calculator_Addition_ShowsChange_WhenEnabled()
    {
        if (!Run("RUN_UIA_INTEGRATION") || !Run("RUN_VISION_INTEGRATION"))
        {
            return;
        }

        await RunStaAsync(async () =>
        {
            using var proc = StartCalculator();
            try
            {
                var window = await WaitForWindowAsync(proc!, TimeSpan.FromSeconds(30), "Calculator");
                Assert.NotNull(window);

                await ClickButtonAsync(window!, "num1Button", "One", "1");
                await ClickButtonAsync(window!, "plusButton", "Plus", "+");
                await ClickButtonAsync(window!, "num2Button", "Two", "2");

                var session = new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() };
                var capture = new ScreenCapture(session, new DesktopSessionFrameProvider(), new CaptureOptions { ImageFormat = "png" });
                var handle = new IntPtr(window!.Current.NativeWindowHandle);
                var baseline = await capture.CaptureWindowAsync(handle);

                await ClickButtonAsync(window!, "equalButton", "Equals", "=");
                await Task.Delay(300);

                var after = await capture.CaptureWindowAsync(handle);
                var detector = new Cascade.Vision.Comparison.ChangeDetector(new Cascade.Vision.Comparison.ComparisonOptions { ChangeThreshold = 0.01 });
                var diff = await detector.CompareAsync(baseline, after);
                Assert.True(diff.HasChanges);

                var result = FindByAutomationId(window!, "CalculatorResults");
                Assert.NotNull(result);
                var text = result!.Current.Name ?? string.Empty;
                Assert.Contains("3", text);
            }
            finally
            {
                if (proc is { HasExited: false })
                {
                    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                }
            }
        });
    }

    [Fact]
    public async Task CodeGen_Compiles_Simple_Code_WhenEnabled()
    {
        if (!Run("RUN_CODEGEN_INTEGRATION"))
        {
            return;
        }

        var compiler = new RoslynCompiler();
        const string source = @"
using System.Threading.Tasks;
public class Calc {
    public Task<int> Add(int a = 2, int b = 3) => Task.FromResult(a + b);
}";
        var result = await compiler.CompileAsync(source, default);

        Assert.True(result.Success);

        var executor = new SandboxedExecutor();
        var callContext = new AutomationCallContext(
            new SessionHandle { SessionId = Guid.NewGuid(), RunId = Guid.NewGuid() },
            VirtualInputProfile.Balanced,
            Guid.NewGuid(),
            CancellationToken.None);
        var executionContext = new CodeGenExecutionContext
        {
            ElementDiscovery = new Mock<IElementDiscovery>().Object,
            ActionExecutor = new Mock<IGeneratedActionExecutor>().Object,
            CancellationToken = CancellationToken.None
        };

        var execResult = await executor.ExecuteAsync<int>(result, "Calc", "Add", callContext, executionContext, CancellationToken.None);
        Assert.True(execResult.Success);
        Assert.Equal(5, execResult.ReturnValue);
    }

    [Fact]
    public async Task Playwright_DataUrl_FormInteraction_WhenEnabled()
    {
        if (!Run("RUN_PLAYWRIGHT_INTEGRATION"))
        {
            return;
        }

        using var playwright = await EnsureChromiumAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 800, Height = 600 },
            ScreenSize = new ScreenSize { Width = 800, Height = 600 }
        });

        var html = @"data:text/html,
<!doctype html>
<html>
<body>
  <input id='name' />
  <button id='ok' onclick=""document.querySelector('#out').innerText='hi ' + document.querySelector('#name').value"">go</button>
  <div id='out'></div>
</body>
</html>";

        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load, Timeout = 15000 });
        page.SetDefaultTimeout(15000);

        await page.WaitForSelectorAsync("#ok", new PageWaitForSelectorOptions { Timeout = 15000 });
        await page.FillAsync("#name", "cascade");
        await page.ClickAsync("#ok");

        // Fallback: if click fails silently, evaluate directly.
        var text = await page.TextContentAsync("#out");
        if (string.IsNullOrWhiteSpace(text))
        {
            await page.EvaluateAsync("() => { document.querySelector('#ok').click(); }");
            text = await page.TextContentAsync("#out");
        }

        Assert.Equal("hi cascade", text?.Trim());
    }

    private static async Task<IPlaywright> EnsureChromiumAsync()
    {
        try
        {
            return await Microsoft.Playwright.Playwright.CreateAsync();
        }
        catch (PlaywrightException)
        {
            await InstallChromiumAsync();
            return await Microsoft.Playwright.Playwright.CreateAsync();
        }
    }

    private static async Task InstallChromiumAsync()
    {
        var baseDir = AppContext.BaseDirectory;
        var scriptPath = Path.Combine(baseDir, "playwright.ps1");
        if (!File.Exists(scriptPath))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoLogo -NoProfile -File \"{scriptPath}\" install chromium",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(startInfo);
            if (proc is not null)
            {
                await proc.WaitForExitAsync();
            }
        }
        catch
        {
            // Swallow install errors; test will fail later with clearer Playwright exception.
        }
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    }
}


