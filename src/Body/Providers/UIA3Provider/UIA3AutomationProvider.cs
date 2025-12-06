using Cascade.Body.Automation;
using Cascade.Body.Configuration;
using Cascade.Body.Vision;
using Cascade.Proto;
using ActionProto = Cascade.Proto.Action;
using StatusProto = Cascade.Proto.Status;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Patterns;
using FlaUI.Core.Definitions;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using Application = FlaUI.Core.Application;
using ProtoImageFormat = Cascade.Proto.ImageFormat;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Cascade.Body.Providers.UIA3Provider;

/// <summary>
/// UIA3-based automation provider for Windows desktop (including Notepad, Calculator, etc).
/// Pattern-first: prefers UIA control patterns (Invoke/Value/Selection/Scroll/Toggle/RangeValue)
/// and falls back to focus+keyboard/mouse only when necessary.
/// </summary>
public class UIA3AutomationProvider : IAutomationProvider, IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly UIA3Options _options;
    private readonly ILogger<UIA3AutomationProvider> _logger;
    private readonly OcrService _ocr;
    private Application? _app;
    private int? _processId;

    public UIA3AutomationProvider(IOptions<UIA3Options> options, ILogger<UIA3AutomationProvider> logger, OcrService ocr)
    {
        _automation = new UIA3Automation();
        _options = options.Value;
        _logger = logger;
        _ocr = ocr;
    }

    public PlatformSource Platform => PlatformSource.Windows;

    public async Task<StatusProto> StartAppAsync(string appName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Launching app via UIA3: {App}", appName);

            // Normalize to an executable name for attach.
            var exeName = Path.GetFileNameWithoutExtension(appName);
            
            // Handle UWP apps where process name differs from executable name
            // e.g., calc.exe launches CalculatorApp process
            string? actualProcessName = null;
            if (string.Equals(exeName, "calc", StringComparison.OrdinalIgnoreCase))
            {
                actualProcessName = "CalculatorApp";
            }

            // If already running, try attach first to avoid multiple instances.
            var existing = FindProcessByName(exeName, actualProcessName);
            
            if (existing != null)
            {
                _logger.LogInformation("Attaching to existing process {Pid} ({Exe})", existing.Id, exeName);
                try
                {
                    _app = Application.Attach(existing);
                    _processId = existing.Id;
                    // Wait a bit for the window to be ready
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    return new StatusProto { Success = true, Message = $"Attached to running {exeName}" };
                }
                catch (Exception attachEx)
                {
                    _logger.LogWarning(attachEx, "Failed to attach to existing process, will try launching new instance");
                    // Continue to launch a new instance
                }
            }

            // Otherwise launch a new instance.
            try
            {
                _app = Application.Launch(appName);
                _processId = _app.ProcessId;
                Console.WriteLine($"[DIAG] Launched app, ProcessId={_processId}");
                
                // Check if process still exists (it might have exited quickly if app was already running)
                try
                {
                    var proc = Process.GetProcessById(_processId.Value);
                    if (proc.HasExited)
                    {
                        Console.WriteLine($"[DIAG] Process {_processId.Value} has already exited, trying to find actual process");
                        // Process exited - for UWP apps, the launcher process exits and the actual app process starts
                        // Wait a bit for the actual process to appear
                        Process? existingProc = null;
                        if (actualProcessName != null)
                        {
                            Console.WriteLine($"[DIAG] Waiting for actual process '{actualProcessName}' to appear...");
                            var waitDeadline = DateTime.UtcNow.AddSeconds(5);
                            while (DateTime.UtcNow < waitDeadline && existingProc == null)
                            {
                                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                                existingProc = FindProcessByName(exeName, actualProcessName);
                            }
                        }
                        else
                        {
                            // For non-UWP apps, just check for existing instance
                            existingProc = FindProcessByName(exeName, actualProcessName);
                        }
                        
                        if (existingProc != null)
                        {
                            Console.WriteLine($"[DIAG] Attaching to existing/actual process {existingProc.Id}");
                            _app?.Dispose();
                            _app = Application.Attach(existingProc);
                            _processId = existingProc.Id;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Process {_processId.Value} exited immediately after launch and no existing/actual process found");
                        }
                    }
                }
                catch (ArgumentException)
                {
                    // Process not found - for UWP apps, the launcher process exits and the actual app process starts
                    // Wait a bit for the actual process to appear
                    if (actualProcessName != null)
                    {
                        Console.WriteLine($"[DIAG] Launcher process {_processId.Value} not found, waiting for actual process '{actualProcessName}' to appear...");
                        var waitDeadline = DateTime.UtcNow.AddSeconds(5);
                        Process? actualProc = null;
                        while (DateTime.UtcNow < waitDeadline && actualProc == null)
                        {
                            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                            actualProc = FindProcessByName(exeName, actualProcessName);
                        }
                        if (actualProc != null)
                        {
                            Console.WriteLine($"[DIAG] Found actual process {actualProc.Id} after waiting");
                            _app?.Dispose();
                            _app = Application.Attach(actualProc);
                            _processId = actualProc.Id;
                        }
                    }
                    
                    // If still not found, try to attach to any existing instance
                    if (_app == null)
                    {
                        Console.WriteLine($"[DIAG] Process {_processId.Value} not found, trying to find existing instance");
                        var existingProc2 = FindProcessByName(exeName, actualProcessName);
                        if (existingProc2 != null)
                        {
                            Console.WriteLine($"[DIAG] Attaching to existing process {existingProc2.Id}");
                            _app = Application.Attach(existingProc2);
                            _processId = existingProc2.Id;
                        }
                    }
                }
                
                // Wait for main window handle with timeout
                bool handleReady = false;
                try
                {
                    handleReady = _app.WaitWhileMainHandleIsMissing(TimeSpan.FromMilliseconds(_options.ActionTimeoutMs));
                    Console.WriteLine($"[DIAG] WaitWhileMainHandleIsMissing returned {handleReady}, MainWindowHandle={_app.MainWindowHandle}");
                }
                catch (Exception waitEx)
                {
                    Console.WriteLine($"[DIAG] WaitWhileMainHandleIsMissing exception: {waitEx.Message}");
                    // Process might have exited - check and attach to existing if needed
                    var existingProc3 = FindProcessByName(exeName, actualProcessName);
                    if (existingProc3 != null && existingProc3.Id != _processId.Value)
                    {
                        Console.WriteLine($"[DIAG] Attaching to existing process {existingProc3.Id} after wait exception");
                        _app?.Dispose();
                        _app = Application.Attach(existingProc3);
                        _processId = existingProc3.Id;
                        handleReady = _app.WaitWhileMainHandleIsMissing(TimeSpan.FromMilliseconds(_options.ActionTimeoutMs));
                    }
                }
                
                // Now actively wait until we can actually get the window via UIA
                var deadline = DateTime.UtcNow.AddMilliseconds(_options.ActionTimeoutMs);
                AutomationElement? window = null;
                while (DateTime.UtcNow < deadline && window == null)
                {
                    try
                    {
                        window = _app.GetMainWindow(_automation, TimeSpan.FromMilliseconds(1000));
                        if (window != null && !window.BoundingRectangle.IsEmpty)
                        {
                            Console.WriteLine($"[DIAG] Successfully got window after launch: Name={window.Name}");
                            break;
                        }
                        window = null;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DIAG] GetMainWindow failed during startup wait: {ex.Message}");
                    }
                    
                    if (window == null)
                    {
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    }
                }
                
                if (window == null)
                {
                    _logger.LogWarning("Could not get main window after launch, but continuing - it may appear later");
                }
            }
            catch (Exception launchEx)
            {
                Console.WriteLine($"[DIAG] Application.Launch exception: {launchEx.Message}");
                _logger.LogWarning(launchEx, "Application.Launch failed for {App}, trying Process.Start as fallback", appName);
                // Fallback to Process.Start and then attach
                try
                {
                    var proc = Process.Start(new ProcessStartInfo(appName) { UseShellExecute = true });
                    if (proc != null)
                    {
                        Console.WriteLine($"[DIAG] Fallback launch: started process {proc.Id}");
                        _processId = proc.Id;
                        // Wait longer and actively check for window
                        var deadline = DateTime.UtcNow.AddMilliseconds(_options.ActionTimeoutMs);
                        while (DateTime.UtcNow < deadline && _app == null)
                        {
                            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                            try
                            {
                                _app = Application.Attach(proc);
                                var window = _app.GetMainWindow(_automation, TimeSpan.FromMilliseconds(1000));
                                if (window != null && !window.BoundingRectangle.IsEmpty)
                                {
                                    Console.WriteLine($"[DIAG] Successfully got window after fallback launch: Name={window.Name}");
                                    break;
                                }
                                _app?.Dispose();
                                _app = null;
                            }
                            catch (Exception attachEx)
                            {
                                Console.WriteLine($"[DIAG] Attach attempt failed: {attachEx.Message}");
                                // Window not ready yet, continue waiting
                            }
                        }
                        
                        if (_app == null)
                        {
                            // Final attach attempt - window might not be ready but process exists
                            Console.WriteLine($"[DIAG] Final attach attempt for process {proc.Id}");
                            _app = Application.Attach(proc);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Process.Start returned null for {appName}");
                    }
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"[DIAG] Fallback launch exception: {fallbackEx.Message}");
                    throw new InvalidOperationException($"Could not launch {appName} using either Application.Launch or Process.Start: {fallbackEx.Message}", fallbackEx);
                }
            }
            
            // Give the window a moment to fully initialize
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            
            return new StatusProto { Success = true, Message = $"Launched {appName}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch {App}", appName);
            return new StatusProto { Success = false, Message = $"Launch failed: {ex.Message}" };
        }
    }

    public async Task<SemanticTree> GetSemanticTreeAsync(CancellationToken cancellationToken)
    {
        var root = await GetRootWindowAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            var msg = $"GetSemanticTreeAsync: no active window. _app is {(_app == null ? "null" : "not null")}, _processId is {(_processId?.ToString() ?? "null")}";
            Console.WriteLine($"[DIAG] {msg}");
            _logger.LogWarning(msg);
            return new SemanticTree();
        }

        try
        {
            var rootName = root.Name ?? "null";
            var rootType = root.ControlType.ToString();
            var msg1 = $"GetSemanticTreeAsync: root window found, ControlType={rootType}, Name={rootName}";
            Console.WriteLine($"[DIAG] {msg1}");
            _logger.LogInformation(msg1);
            
            var rect = root.BoundingRectangle;
            var msg2 = $"Root bounding rect: Left={rect.Left}, Top={rect.Top}, Width={rect.Width}, Height={rect.Height}, IsEmpty={rect.IsEmpty}";
            Console.WriteLine($"[DIAG] {msg2}");
            _logger.LogInformation(msg2);
            
            var elements = new List<UIElement>();
            await TraverseAsync(root, 0, elements, cancellationToken).ConfigureAwait(false);
            
            var msg3 = $"GetSemanticTreeAsync: traversed {elements.Count} elements";
            Console.WriteLine($"[DIAG] {msg3}");
            _logger.LogInformation(msg3);
            
            return new SemanticTree { Elements = { elements } };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DIAG] Exception in GetSemanticTreeAsync: {ex}");
            _logger.LogError(ex, "Exception in GetSemanticTreeAsync");
            return new SemanticTree();
        }
    }

    public async Task<StatusProto> PerformActionAsync(ActionProto action, CancellationToken cancellationToken)
    {
        var root = await GetRootWindowAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            Console.WriteLine("[DIAG] PerformActionAsync: No active window");
            return new StatusProto { Success = false, Message = "No active window" };
        }

        Console.WriteLine($"[DIAG] PerformActionAsync: Looking for element with Selector ControlType={action.Selector?.ControlType}, Name={action.Selector?.Name}, Id={action.Selector?.Id}");
        var target = await FindElementAsync(root, action.Selector, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            Console.WriteLine("[DIAG] PerformActionAsync: Element not found");
            return new StatusProto { Success = false, Message = "Element not found" };
        }
        
        Console.WriteLine($"[DIAG] PerformActionAsync: Found element, ControlType={target.ControlType}, Name={target.Name}, performing action {action.ActionType}");

        try
        {
            Console.WriteLine($"[DIAG] PerformActionAsync: Calling TryHandleWithPatterns");
            var handled = TryHandleWithPatterns(target, action);
            Console.WriteLine($"[DIAG] PerformActionAsync: TryHandleWithPatterns returned {handled}");
            if (!handled)
            {
                Console.WriteLine($"[DIAG] PerformActionAsync: Pattern failed, trying fallback");
                handled = await FallbackInputAsync(target, action, cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"[DIAG] PerformActionAsync: FallbackInputAsync returned {handled}");
            }

            var result = handled
                ? new StatusProto { Success = true, Message = "OK" }
                : new StatusProto { Success = false, Message = $"Action not supported or failed: {action.ActionType}" };
            Console.WriteLine($"[DIAG] PerformActionAsync: Returning Success={result.Success}, Message={result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DIAG] PerformActionAsync: Exception caught: {ex.Message}");
            _logger.LogError(ex, "Action failed");
            return new StatusProto { Success = false, Message = $"Action failed: {ex.Message}" };
        }
    }

    public async Task<Screenshot> GetMarkedScreenshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var img = Capture.Screen();
            using var ms = new MemoryStream();
            img.Bitmap.Save(ms, DrawingImageFormat.Png);
            return new Screenshot
            {
                Image = Google.Protobuf.ByteString.CopyFrom(ms.ToArray()),
                Format = ProtoImageFormat.Png
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Screenshot failed");
            return new Screenshot { Format = ProtoImageFormat.Png };
        }
    }

    public bool SupportsPatternFirst(Selector selector) => true;

    private static Process? FindProcessByName(string exeName, string? actualProcessName)
    {
        // Try actual process name first (for UWP apps)
        if (actualProcessName != null)
        {
            var proc = Process.GetProcessesByName(actualProcessName).FirstOrDefault();
            if (proc != null) return proc;
        }
        // Fallback to executable name
        return Process.GetProcessesByName(exeName).FirstOrDefault();
    }

    private async Task<AutomationElement?> GetRootWindowAsync(CancellationToken cancellationToken)
    {
        if (_app == null)
        {
            Console.WriteLine("[DIAG] GetRootWindowAsync: _app is null");
            _logger.LogWarning("GetRootWindowAsync: _app is null");
            return null;
        }

        Console.WriteLine($"[DIAG] GetRootWindowAsync: _app is not null, _processId={_processId}, MainWindowHandle={_app.MainWindowHandle}");

        // Retry getting the main window with exponential backoff
        var maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var timeout = TimeSpan.FromMilliseconds(Math.Min(5000, _options.ActionTimeoutMs / (maxAttempts - attempt)));
                Console.WriteLine($"[DIAG] Attempt {attempt + 1}/{maxAttempts}: calling GetMainWindow with timeout {timeout.TotalMilliseconds}ms");
                var window = _app.GetMainWindow(_automation, timeout);
                if (window != null && !window.BoundingRectangle.IsEmpty)
                {
                    Console.WriteLine($"[DIAG] Successfully got main window on attempt {attempt + 1}");
                    _logger.LogDebug("Successfully got main window on attempt {Attempt}", attempt + 1);
                    return window;
                }
                
                if (window == null)
                {
                    Console.WriteLine($"[DIAG] GetMainWindow returned null on attempt {attempt + 1}");
                    _logger.LogDebug("GetMainWindow returned null on attempt {Attempt}, waiting before retry", attempt + 1);
                }
                else if (window.BoundingRectangle.IsEmpty)
                {
                    Console.WriteLine($"[DIAG] Main window has empty bounding rectangle on attempt {attempt + 1}");
                    _logger.LogDebug("Main window has empty bounding rectangle on attempt {Attempt}, waiting before retry", attempt + 1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DIAG] Exception on attempt {attempt + 1}: {ex.Message}");
                _logger.LogDebug(ex, "Failed to get main window on attempt {Attempt}", attempt + 1);
            }

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(500 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        // Last resort: try to find window by process ID from desktop
        if (_processId.HasValue)
        {
            try
            {
                Console.WriteLine($"[DIAG] Trying desktop search by process ID {_processId.Value}");
                var desktop = _automation.GetDesktop();
                var cf = new ConditionFactory(new UIA3PropertyLibrary());
                var windows = desktop.FindAllChildren(cf.ByProcessId(_processId.Value));
                Console.WriteLine($"[DIAG] Desktop search found {windows.Length} windows for process {_processId.Value}");
                
                // Try to find a window with non-empty bounding rectangle
                var window = windows.FirstOrDefault(w => !w.BoundingRectangle.IsEmpty);
                
                // If no window found, try filtering by ControlType.Window
                if (window == null)
                {
                    Console.WriteLine("[DIAG] No window with non-empty bounding rectangle, trying to find Window control type");
                    window = windows.FirstOrDefault(w => w.ControlType == FlaUI.Core.Definitions.ControlType.Window);
                }
                
                // If still not found, try finding by name containing "Calculator"
                if (window == null)
                {
                    Console.WriteLine("[DIAG] Trying to find window by name containing 'Calculator'");
                    foreach (var w in windows)
                    {
                        try
                        {
                            var name = w.Name ?? string.Empty;
                            if (name.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
                            {
                                window = w;
                                Console.WriteLine($"[DIAG] Found window by name: '{name}'");
                                break;
                            }
                        }
                        catch { }
                    }
                }
                
                // Last resort: just take the first window if available
                if (window == null && windows.Length > 0)
                {
                    Console.WriteLine($"[DIAG] Using first available window (of {windows.Length})");
                    window = windows[0];
                }
                
                if (window != null)
                {
                    Console.WriteLine($"[DIAG] Found window via desktop search: Name='{window.Name}', ControlType={window.ControlType}, Rect.IsEmpty={window.BoundingRectangle.IsEmpty}");
                    _logger.LogDebug("Found window by process ID from desktop");
                    return window;
                }
                else
                {
                    Console.WriteLine("[DIAG] No suitable window found in desktop search");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DIAG] Exception in desktop search: {ex.Message}");
                _logger.LogDebug(ex, "Failed to find window by process ID from desktop");
            }
        }

        // For UWP apps, try searching by window name as last resort
        try
        {
            Console.WriteLine("[DIAG] Trying to find window by name search (for UWP apps)");
            var desktop = _automation.GetDesktop();
            var cf = new ConditionFactory(new UIA3PropertyLibrary());
            
            // Try to find Calculator window by name
            var allWindows = desktop.FindAllDescendants(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
            Console.WriteLine($"[DIAG] Found {allWindows.Length} total windows in desktop");
            
            foreach (var win in allWindows)
            {
                try
                {
                    var name = win.Name ?? string.Empty;
                    if (name.Contains("Calculator", StringComparison.OrdinalIgnoreCase) && !win.BoundingRectangle.IsEmpty)
                    {
                        Console.WriteLine($"[DIAG] Found Calculator window by name search: '{name}'");
                        _logger.LogDebug("Found Calculator window by name search");
                        return win;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DIAG] Exception in name-based window search: {ex.Message}");
            _logger.LogDebug(ex, "Failed name-based window search");
        }

        Console.WriteLine($"[DIAG] Failed to get main window after {maxAttempts} attempts and all fallback searches");
        _logger.LogWarning("Failed to get main window after {Attempts} attempts and all fallback searches", maxAttempts);
        return null;
    }

    private async Task TraverseAsync(AutomationElement element, int depth, List<UIElement> output, CancellationToken cancellationToken)
    {
        if (output.Count >= _options.MaxNodes || depth > _options.TreeDepth)
        {
            return;
        }

        var rect = element.BoundingRectangle;
        if (rect.IsEmpty)
        {
            return;
        }

        var uiElement = await BuildUiElementAsync(element, cancellationToken).ConfigureAwait(false);
        output.Add(uiElement);

        AutomationElement[] children;
        try
        {
            children = element.FindAllChildren();
            if (depth == 0)
            {
                var msg1 = $"TraverseAsync root: found {children.Length} children";
                Console.WriteLine($"[DIAG] {msg1}");
                _logger.LogInformation(msg1);
                for (int i = 0; i < Math.Min(children.Length, 5); i++)
                {
                    var child = children[i];
                    var childRect = child.BoundingRectangle;
                    var childName = child.Name ?? "null";
                    var msg2 = $"  Child {i}: Name={childName}, ControlType={child.ControlType}, Rect.IsEmpty={childRect.IsEmpty}";
                    Console.WriteLine($"[DIAG] {msg2}");
                    _logger.LogInformation(msg2);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find children at depth {Depth}", depth);
            return;
        }
        
        foreach (var child in children)
        {
            try
            {
                await TraverseAsync(child, depth + 1, output, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to traverse child at depth {Depth}", depth + 1);
            }
        }
    }

    private async Task<UIElement> BuildUiElementAsync(AutomationElement element, CancellationToken cancellationToken)
    {
        var rect = element.BoundingRectangle;
        var runtimeId = element.Properties.RuntimeId?.ValueOrDefault;
        string? automationId = null;
        try
        {
            automationId = element.Properties.AutomationId?.TryGetValue(out var aid) == true ? aid : null;
        }
        catch
        {
            automationId = null;
        }

        string? name = null;
        try
        {
            name = element.Properties.Name?.TryGetValue(out var nm) == true ? nm : null;
        }
        catch
        {
            name = null;
        }

        string? value = null;
        try
        {
            var valuePattern = element.Patterns.Value.PatternOrDefault;
            if (valuePattern != null && !valuePattern.IsReadOnly)
            {
                value = valuePattern.Value;
            }
        }
        catch
        {
            value = null;
        }

        var uiElement = new UIElement
        {
            Id = runtimeId != null && runtimeId.Length > 0 ? runtimeId[0].ToString() : automationId ?? Guid.NewGuid().ToString(),
            Name = name ?? string.Empty,
            ControlType = MapControlType(element.ControlType),
            BoundingBox = NormalizationHelpers.ToNormalizedRectangle(
                new System.Drawing.RectangleF((float)rect.Left, (float)rect.Top, (float)rect.Width, (float)rect.Height)),
            ParentId = string.Empty,
            PlatformSource = Platform
        };

        if (!string.IsNullOrWhiteSpace(automationId))
        {
            uiElement.AutomationId = automationId;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            uiElement.ValueText = value;
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            uiElement.ValueText = name;
        }

        if (_ocr.IsEnabled && string.IsNullOrWhiteSpace(uiElement.Name))
        {
            try
            {
                using var cap = Capture.Element(element);
                using var bmp = cap.Bitmap;
                var ocrResult = await _ocr.ExtractFromBitmapAsync(bmp, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                {
                    uiElement.Name = ocrResult.Text;
                    uiElement.ValueText = ocrResult.Text;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "OCR failed for element");
            }
        }

        return uiElement;
    }

    private Task<AutomationElement?> FindElementAsync(AutomationElement root, Selector selector, CancellationToken cancellationToken)
    {
        if (selector == null)
        {
            return Task.FromResult<AutomationElement?>(null);
        }

        AutomationElement current = root;

        // Optional path-based narrowing.
        if (selector.Path.Count > 0)
        {
            foreach (var segment in selector.Path)
            {
                current = current.FindFirstDescendant(cf => cf.ByAutomationId(segment)) ??
                          current.FindFirstDescendant(cf => cf.ByName(segment));
                if (current == null)
                {
                    break;
                }
            }
        }

        if (current == null)
        {
            return Task.FromResult<AutomationElement?>(null);
        }

        var candidates = current.FindAllDescendants();
        Console.WriteLine($"[DIAG] FindElementAsync: Found {candidates.Length} candidate descendants");
        
        // Debug: show control types present (including Edit/Document if looking for Input)
        if (selector.ControlType == Cascade.Proto.ControlType.Input)
        {
            var editLike = candidates.Where(e => 
            {
                var ct = e.ControlType;
                return ct == FlaUI.Core.Definitions.ControlType.Edit || 
                       ct == FlaUI.Core.Definitions.ControlType.Document ||
                       ct == FlaUI.Core.Definitions.ControlType.Text;
            }).Take(5).ToList();
            Console.WriteLine($"[DIAG]   Found {editLike.Count} Edit/Document/Text elements (potential Input matches):");
            foreach (var e in editLike)
            {
                string? name = null;
                string? autoId = null;
                try { name = e.Name; } catch { name = "error"; }
                try { autoId = e.AutomationId; } catch { autoId = "error"; }
                Console.WriteLine($"[DIAG]     ControlType={e.ControlType}, Name={name ?? "null"}, AutomationId={autoId ?? "null"}");
            }
        }
        
        var controlTypes = candidates.Select(e => e.ControlType).Distinct().Take(15).ToList();
        foreach (var ct in controlTypes)
        {
            var mapped = MapControlType(ct);
            Console.WriteLine($"[DIAG]   ControlType in tree: {ct} (mapped to {mapped})");
        }
        
        // Convert to list to avoid multiple enumerations and ensure we can count properly
        var candidatesList = candidates.ToList();
        Console.WriteLine($"[DIAG] Initial candidates count: {candidatesList.Count}");

        // Build filter conditions
        var filtered = candidatesList.Where(e =>
        {
            // Id filter
            if (!string.IsNullOrWhiteSpace(selector.Id))
            {
                try
                {
                    if (!string.Equals(e.AutomationId, selector.Id, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                catch
                {
                    return false;
                }
            }

            // Name filter
            if (!string.IsNullOrWhiteSpace(selector.Name))
            {
                try
                {
                    if (!string.Equals(e.Name, selector.Name, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                catch
                {
                    return false;
                }
            }

            // ControlType filter
            if (selector.ControlType != Cascade.Proto.ControlType.Unspecified)
            {
                try
                {
                    var mapped = MapControlType(e.ControlType);
                    bool typeMatches = mapped == selector.ControlType;
                    
                    // Special case: Input can match Edit, Document, or Text control types
                    if (!typeMatches && selector.ControlType == Cascade.Proto.ControlType.Input)
                    {
                        typeMatches = e.ControlType == FlaUI.Core.Definitions.ControlType.Edit ||
                                     e.ControlType == FlaUI.Core.Definitions.ControlType.Document ||
                                     e.ControlType == FlaUI.Core.Definitions.ControlType.Text;
                    }
                    
                    if (!typeMatches)
                        return false;
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }).ToList();
        
        Console.WriteLine($"[DIAG] After all filters: {candidatesList.Count} -> {filtered.Count}");
        // Debug: show some matching elements
        foreach (var match in filtered.Take(3))
        {
            string? matchName = null;
            string? matchAutoId = null;
            try { matchName = match.Name; } catch { matchName = "error"; }
            try { matchAutoId = match.AutomationId; } catch { matchAutoId = "error"; }
            Console.WriteLine($"[DIAG]   Match: ControlType={match.ControlType}, Name={matchName ?? "null"}, AutomationId={matchAutoId ?? "null"}");
        }

        var list = filtered;
        
        // If we have multiple matches and are looking for Input, prioritize Edit/Document over Text
        if (list.Count > 1 && selector.ControlType == Cascade.Proto.ControlType.Input)
        {
            var prioritized = list.Where(e => 
                e.ControlType == FlaUI.Core.Definitions.ControlType.Edit ||
                e.ControlType == FlaUI.Core.Definitions.ControlType.Document).ToList();
            if (prioritized.Count > 0)
            {
                Console.WriteLine($"[DIAG] FindElementAsync: Prioritizing {prioritized.Count} Edit/Document elements from {list.Count} total matches");
                list = prioritized;
            }
        }
        
        if (list.Count == 0)
        {
            Console.WriteLine("[DIAG] FindElementAsync: No elements matched all filters");
            
            // Fallback: if looking for Input and no matches, try to find any Edit/Document/Text element
            if (selector.ControlType == Cascade.Proto.ControlType.Input)
            {
                Console.WriteLine("[DIAG] FindElementAsync: Trying fallback - looking for any Edit/Document/Text element");
                var fallback = candidatesList.FirstOrDefault(e =>
                {
                    var ct = e.ControlType;
                    return ct == FlaUI.Core.Definitions.ControlType.Edit ||
                           ct == FlaUI.Core.Definitions.ControlType.Document ||
                           ct == FlaUI.Core.Definitions.ControlType.Text;
                });
                if (fallback != null)
                {
                    Console.WriteLine($"[DIAG] FindElementAsync: Found fallback element: ControlType={fallback.ControlType}, Name={fallback.Name ?? "null"}");
                    return Task.FromResult<AutomationElement?>(fallback);
                }
            }
            
            return Task.FromResult<AutomationElement?>(null);
        }
        
        Console.WriteLine($"[DIAG] FindElementAsync: {list.Count} elements matched, selecting index {selector.Index}");

        var index = selector.Index;
        if (index < 0) index = 0;
        if (index >= list.Count) index = list.Count - 1;

        var selected = list[index];
        string? selName = null;
        string? selAutoId = null;
        try { selName = selected.Name; } catch { }
        try { selAutoId = selected.AutomationId; } catch { }
        Console.WriteLine($"[DIAG] FindElementAsync: Selected element at index {index}: ControlType={selected.ControlType}, Name={selName ?? "null"}, AutomationId={selAutoId ?? "null"}");
        
        return Task.FromResult<AutomationElement?>(selected);
    }

    private bool TryHandleWithPatterns(AutomationElement target, ActionProto action)
    {
        // Expand collapsed items to ensure interaction.
        UiaPatterns.TryExpandForActionType(target, action.ActionType);

        switch (action.ActionType)
        {
            case ActionType.Click:
                return UiaPatterns.TryInvoke(target) ||
                       UiaPatterns.TrySelectionItem(target) ||
                       UiaPatterns.TryLegacyAccessibleAction(target);

            case ActionType.TypeText:
                var text = action.Text ?? string.Empty;
                Console.WriteLine($"[DIAG] TryHandleWithPatterns TypeText: trying ValuePattern first");
                if (UiaPatterns.TrySetValue(target, text))
                {
                    Console.WriteLine($"[DIAG] TryHandleWithPatterns TypeText: ValuePattern succeeded");
                    return true;
                }
                Console.WriteLine($"[DIAG] TryHandleWithPatterns TypeText: ValuePattern failed, trying TextPattern");
                if (UiaPatterns.TryTextInsert(target, text))
                {
                    Console.WriteLine($"[DIAG] TryHandleWithPatterns TypeText: TextPattern succeeded");
                    return true;
                }
                Console.WriteLine($"[DIAG] TryHandleWithPatterns TypeText: both patterns failed, will fallback to keyboard");
                return false;

            case ActionType.Focus:
                return UiaPatterns.TrySelectionItem(target) || FocusElement(target);

            case ActionType.Scroll:
                return UiaPatterns.TryScrollItem(target) ||
                       UiaPatterns.TryScroll(target, null, action.Number);

            case ActionType.WaitVisible:
                target.WaitUntilClickable(TimeSpan.FromMilliseconds(_options.ActionTimeoutMs));
                return true;

            default:
                return false;
        }
    }

    private async Task<bool> FallbackInputAsync(AutomationElement target, ActionProto action, CancellationToken cancellationToken)
    {
        try
        {
            switch (action.ActionType)
            {
                case ActionType.Click:
                    target.WaitUntilClickable(TimeSpan.FromMilliseconds(_options.ActionTimeoutMs));
                    target.Click(true);
                    return true;
                case ActionType.TypeText:
                    Console.WriteLine($"[DIAG] FallbackInputAsync TypeText: using keyboard input");
                    target.WaitUntilClickable(TimeSpan.FromMilliseconds(_options.ActionTimeoutMs));
                    target.Focus();
                    var textToType = action.Text ?? string.Empty;
                    Console.WriteLine($"[DIAG] FallbackInputAsync TypeText: typing '{textToType}'");
                    Keyboard.Type(textToType);
                    Console.WriteLine($"[DIAG] FallbackInputAsync TypeText: keyboard input completed");
                    return true;
                case ActionType.Hover:
                    var pt = target.GetClickablePoint();
                    Mouse.MoveTo(pt);
                    return true;
                case ActionType.Focus:
                    return FocusElement(target);
                case ActionType.Scroll:
                    MouseWheel(action.Number != 0 ? action.Number : 120);
                    return true;
                case ActionType.WaitVisible:
                    target.WaitUntilClickable(TimeSpan.FromMilliseconds(_options.ActionTimeoutMs));
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallback input failed for {Action}", action.ActionType);
            return false;
        }
    }

    private bool FocusElement(AutomationElement element)
    {
        try
        {
            element.Focus();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private FlaUI.Core.Definitions.ControlType? MapControlType(Cascade.Proto.ControlType controlType) =>
        controlType switch
        {
            Cascade.Proto.ControlType.Button => FlaUI.Core.Definitions.ControlType.Button,
            Cascade.Proto.ControlType.Input => FlaUI.Core.Definitions.ControlType.Edit,
            Cascade.Proto.ControlType.Combo => FlaUI.Core.Definitions.ControlType.ComboBox,
            Cascade.Proto.ControlType.Menu => FlaUI.Core.Definitions.ControlType.MenuItem,
            Cascade.Proto.ControlType.Tree => FlaUI.Core.Definitions.ControlType.TreeItem,
            Cascade.Proto.ControlType.Table => FlaUI.Core.Definitions.ControlType.DataGrid,
            Cascade.Proto.ControlType.Custom => FlaUI.Core.Definitions.ControlType.Custom,
            _ => null
        };

    private Cascade.Proto.ControlType MapControlType(FlaUI.Core.Definitions.ControlType? controlType)
    {
        if (controlType == null) return Cascade.Proto.ControlType.Custom;

        if (controlType == FlaUI.Core.Definitions.ControlType.Button) return Cascade.Proto.ControlType.Button;
        if (controlType == FlaUI.Core.Definitions.ControlType.Edit) return Cascade.Proto.ControlType.Input;
        if (controlType == FlaUI.Core.Definitions.ControlType.ComboBox) return Cascade.Proto.ControlType.Combo;
        if (controlType == FlaUI.Core.Definitions.ControlType.MenuItem) return Cascade.Proto.ControlType.Menu;
        if (controlType == FlaUI.Core.Definitions.ControlType.TreeItem) return Cascade.Proto.ControlType.Tree;
        if (controlType == FlaUI.Core.Definitions.ControlType.DataGrid) return Cascade.Proto.ControlType.Table;
        return Cascade.Proto.ControlType.Custom;
    }

    private static void MouseWheel(double amount)
    {
        var delta = (int)Math.Clamp(amount, -1200, 1200);
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
    }

    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    public void Dispose()
    {
        _automation.Dispose();
        _app?.Dispose();
    }
}


