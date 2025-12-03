# UI Automation Module Specification

## Overview

The `Cascade.UIAutomation` module provides a high-level wrapper around Microsoft UI Automation (UIA) framework, enabling programmatic discovery and interaction with Windows application UI elements. All automation happens inside hidden Windows Virtual Desktop sessions using virtual keyboard/mouse devices so the user’s physical desktop remains untouched while agents explore or execute tasks.

## Dependencies

```xml
<PackageReference Include="Microsoft.Windows.SDK.Contracts" Version="10.0.22621.755" />
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
<PackageReference Include="Vanara.PInvoke.VirtualDesktop" Version="3.4.14" />
<PackageReference Include="WindowsInput" Version="9.0.0" />
```

## Architecture

```
Cascade.UIAutomation/
├── Elements/
│   ├── IUIElement.cs           # Element abstraction interface
│   ├── UIElement.cs            # Concrete implementation
│   ├── ElementFactory.cs       # Element creation
│   └── ElementCache.cs         # Caching layer
├── Session/
│   ├── SessionHandle.cs        # Hidden desktop session token
│   ├── SessionContext.cs       # Ambient session data
│   └── SessionRouter.cs        # Routes calls to correct desktop
├── Input/
│   ├── IVirtualInputProvider.cs # Virtual mouse/keyboard abstraction
│   ├── VirtualMouse.cs         # HID injection
│   └── VirtualKeyboard.cs      # HID injection
├── Actions/
│   ├── IActionExecutor.cs      # Action interface
│   ├── ClickAction.cs          # Mouse click operations
│   ├── TypeAction.cs           # Keyboard input
│   ├── ScrollAction.cs         # Scroll operations
│   └── DragDropAction.cs       # Drag and drop
├── TreeWalker/
│   ├── ITreeWalker.cs          # Tree navigation interface
│   ├── UITreeWalker.cs         # UIA tree walker
│   ├── FilteredTreeWalker.cs   # Filtered navigation
│   └── TreeSnapshot.cs         # Point-in-time snapshot
├── Patterns/
│   ├── IPatternProvider.cs     # Pattern abstraction
│   ├── InvokePattern.cs        # Button/link activation
│   ├── ValuePattern.cs         # Text input
│   ├── SelectionPattern.cs     # List/combo selection
│   ├── TogglePattern.cs        # Checkbox/toggle
│   ├── ExpandCollapsePattern.cs # Tree nodes
│   └── ScrollPattern.cs        # Scroll containers
├── Discovery/
│   ├── IElementDiscovery.cs    # Discovery interface
│   ├── ElementDiscovery.cs     # Find elements
│   ├── SearchCriteria.cs       # Search parameters
│   └── ElementLocator.cs       # XPath-like locators
├── Windows/
│   ├── IWindowManager.cs       # Window management interface
│   ├── WindowManager.cs        # Window operations
│   └── ProcessAttachment.cs    # Process binding
└── Services/
    ├── UIAutomationService.cs  # Main service facade
    └── UIAutomationOptions.cs  # Configuration
```

## Session Context & Hidden Desktop Integration

- Every `Cascade.UIAutomation` service instance is **session-bound**. A `SessionHandle` identifies the hidden Windows Virtual Desktop, virtual display, and input pipeline used for automation.
- Sessions are provisioned by the Session Orchestrator; UIAutomation simply consumes the handle and routes all UIA calls through the correct desktop.
- The module uses virtual HID drivers so mouse/keyboard events never reach the user’s physical devices.

```csharp
public sealed record SessionHandle(
    Guid SessionId,
    IntPtr VirtualDesktopId,
    string UserProfilePath);

public interface ISessionContextAccessor
{
    SessionHandle Session { get; }
    VirtualInputChannel InputChannel { get; }
    IAutomationElement RootElement { get; }
}
```

> **Concurrency**: Multiple sessions can be active simultaneously. Each `SessionHandle` carries throttling metadata so the backend can pause automation without affecting other sessions or the end user.

## Core Interfaces

### IUIElement

```csharp
public interface IUIElement
{
    // Session context
    SessionHandle Session { get; }
    VirtualInputChannel InputChannel { get; }

    // Identity
    string AutomationId { get; }
    string Name { get; }
    string ClassName { get; }
    ControlType ControlType { get; }
    string RuntimeId { get; }
    
    // Hierarchy
    IUIElement? Parent { get; }
    IReadOnlyList<IUIElement> Children { get; }
    IUIElement? FindFirst(SearchCriteria criteria);
    IReadOnlyList<IUIElement> FindAll(SearchCriteria criteria);
    
    // Geometry
    Rectangle BoundingRectangle { get; }
    Point ClickablePoint { get; }
    bool IsOffscreen { get; }
    
    // State
    bool IsEnabled { get; }
    bool HasKeyboardFocus { get; }
    bool IsContentElement { get; }
    bool IsControlElement { get; }
    
    // Patterns
    bool TryGetPattern<T>(out T pattern) where T : class;
    IReadOnlyList<PatternType> SupportedPatterns { get; }
    
    // Actions
    Task ClickAsync(ClickType clickType = ClickType.Left);
    Task DoubleClickAsync();
    Task RightClickAsync();
    Task TypeTextAsync(string text);
    Task SetValueAsync(string value);
    Task InvokeAsync();
    Task SetFocusAsync();
    
    // Serialization
    ElementSnapshot ToSnapshot();
}
```

### IElementDiscovery

> All discovery operations are implicitly scoped to the `SessionHandle` supplied when the service is resolved. The interface remains simple while providing overloads that accept an explicit handle when needed.

```csharp
public interface IElementDiscovery
{
    // Desktop root
    IUIElement GetDesktopRoot();
    
    // Find by window
    IUIElement? GetForegroundWindow();
    IUIElement? FindWindow(string title);
    IUIElement? FindWindow(Func<IUIElement, bool> predicate);
    IReadOnlyList<IUIElement> GetAllWindows();
    
    // Find by process
    IUIElement? GetMainWindow(int processId);
    IUIElement? GetMainWindow(string processName);
    
    // Global search
    IUIElement? FindElement(SearchCriteria criteria, TimeSpan? timeout = null);
    IReadOnlyList<IUIElement> FindAllElements(SearchCriteria criteria);
    
    // Wait operations
    Task<IUIElement?> WaitForElementAsync(SearchCriteria criteria, TimeSpan timeout);
    Task<bool> WaitForElementGoneAsync(SearchCriteria criteria, TimeSpan timeout);
}
```

### ITreeWalker

```csharp
public interface ITreeWalker
{
    // Navigation
    IUIElement? GetParent(IUIElement element);
    IUIElement? GetFirstChild(IUIElement element);
    IUIElement? GetLastChild(IUIElement element);
    IUIElement? GetNextSibling(IUIElement element);
    IUIElement? GetPreviousSibling(IUIElement element);
    
    // Enumeration
    IEnumerable<IUIElement> GetChildren(IUIElement element);
    IEnumerable<IUIElement> GetDescendants(IUIElement element, int maxDepth = -1);
    IEnumerable<IUIElement> GetAncestors(IUIElement element);
    
    // Filtered walking
    ITreeWalker WithFilter(Func<IUIElement, bool> filter);
    ITreeWalker ControlViewWalker { get; }
    ITreeWalker ContentViewWalker { get; }
    ITreeWalker RawViewWalker { get; }
    
    // Snapshots
    TreeSnapshot CaptureSnapshot(IUIElement root, int maxDepth = -1);
}
```

## Search Criteria

```csharp
public class SearchCriteria
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? NameContains { get; set; }
    public string? ClassName { get; set; }
    public ControlType? ControlType { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsOffscreen { get; set; }
    public Rectangle? BoundingRectangle { get; set; }
    
    // Composite criteria
    public SearchCriteria? And(SearchCriteria other);
    public SearchCriteria? Or(SearchCriteria other);
    public SearchCriteria? Not();
    
    // Builder pattern
    public static SearchCriteria ByAutomationId(string id);
    public static SearchCriteria ByName(string name);
    public static SearchCriteria ByClassName(string className);
    public static SearchCriteria ByControlType(ControlType type);
}
```

## Element Locators

XPath-like syntax for element location:

```csharp
public class ElementLocator
{
    // Parse locator string
    public static ElementLocator Parse(string locator);
    
    // Find element using locator
    public IUIElement? Find(IUIElement root);
    public IReadOnlyList<IUIElement> FindAll(IUIElement root);
}

// Locator syntax examples:
// "/Window[@Name='Calculator']/Button[@AutomationId='num1Button']"
// "//Button[contains(@Name, 'Submit')]"
// "/Window/Pane/Edit[@ClassName='TextBox'][1]"
```

## Virtual Input Provider

All user actions (clicks, typing, scrolling) are executed through a `IVirtualInputProvider` that is bound to the session’s virtual desktop. This guarantees:
- No physical mouse/keyboard hijacking.
- Pointer coordinates are mapped to the hidden desktop resolution.
- Input latency and retries can be tuned per session.

```csharp
public interface IVirtualInputProvider
{
    SessionHandle Session { get; }

    Task MoveMouseAsync(Point screenPoint);
    Task ClickAsync(MouseButton button, ClickOptions? options = null);
    Task TypeTextAsync(string text, TextEntryOptions? options = null);
    Task SendVirtualKeyAsync(VirtualKey key, KeySendOptions? options = null);
    Task ScrollAsync(int delta, ScrollOptions? options = null);
}
```

## UI Patterns

### InvokePattern
```csharp
public interface IInvokePattern
{
    Task InvokeAsync();
}
```

### ValuePattern
```csharp
public interface IValuePattern
{
    string Value { get; }
    bool IsReadOnly { get; }
    Task SetValueAsync(string value);
}
```

### SelectionPattern
```csharp
public interface ISelectionPattern
{
    IReadOnlyList<IUIElement> GetSelection();
    bool CanSelectMultiple { get; }
    bool IsSelectionRequired { get; }
}

public interface ISelectionItemPattern
{
    bool IsSelected { get; }
    IUIElement SelectionContainer { get; }
    Task SelectAsync();
    Task AddToSelectionAsync();
    Task RemoveFromSelectionAsync();
}
```

### TogglePattern
```csharp
public interface ITogglePattern
{
    ToggleState ToggleState { get; }
    Task ToggleAsync();
}
```

### ExpandCollapsePattern
```csharp
public interface IExpandCollapsePattern
{
    ExpandCollapseState State { get; }
    Task ExpandAsync();
    Task CollapseAsync();
}
```

### ScrollPattern
```csharp
public interface IScrollPattern
{
    double HorizontalScrollPercent { get; }
    double VerticalScrollPercent { get; }
    double HorizontalViewSize { get; }
    double VerticalViewSize { get; }
    bool HorizontallyScrollable { get; }
    bool VerticallyScrollable { get; }
    
    Task ScrollAsync(ScrollAmount horizontal, ScrollAmount vertical);
    Task SetScrollPercentAsync(double horizontal, double vertical);
}
```

## Window Management

```csharp
public interface IWindowManager
{
    // Window state
    Task<bool> SetForegroundAsync(IUIElement window);
    Task MinimizeAsync(IUIElement window);
    Task MaximizeAsync(IUIElement window);
    Task RestoreAsync(IUIElement window);
    Task CloseAsync(IUIElement window);
    
    // Window geometry
    Task MoveAsync(IUIElement window, int x, int y);
    Task ResizeAsync(IUIElement window, int width, int height);
    
    // Process management
    IUIElement? AttachToProcess(int processId);
    IUIElement? AttachToProcess(string processName);
    IUIElement? LaunchAndAttach(string executablePath, string? arguments = null);
}
```

> Window state changes are applied **inside the hidden desktop**. Bringing a window to the foreground means activating it within the virtual session, not the user’s actual desktop. When needed, the orchestrator can mirror the final window position back to the user workspace after automation completes.

## Element Caching

```csharp
public class ElementCache
{
    public SessionHandle Session { get; }

    // Cache configuration
    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxCachedElements { get; set; } = 1000;
    
    // Cache operations
    public IUIElement? GetCached(string runtimeId);
    public void Cache(IUIElement element, TimeSpan? duration = null);
    public void Invalidate(string runtimeId);
    public void InvalidateAll();
    
    // Staleness detection
    public bool IsStale(IUIElement element);
    public Task<IUIElement?> RefreshAsync(IUIElement element);
}
```

## Tree Snapshots

```csharp
public class TreeSnapshot
{
    public ElementSnapshot Root { get; }
    public DateTime CapturedAt { get; }
    public int TotalElements { get; }
    
    // Search within snapshot
    public ElementSnapshot? FindByRuntimeId(string runtimeId);
    public ElementSnapshot? FindByAutomationId(string automationId);
    public IReadOnlyList<ElementSnapshot> FindByControlType(ControlType type);
    
    // Serialization
    public string ToJson();
    public static TreeSnapshot FromJson(string json);
}

public class ElementSnapshot
{
    public string RuntimeId { get; set; }
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? ClassName { get; set; }
    public string ControlType { get; set; }
    public Rectangle BoundingRectangle { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsOffscreen { get; set; }
    public List<string> SupportedPatterns { get; set; }
    public List<ElementSnapshot> Children { get; set; }
}
```

## Service Configuration

```csharp
public class UIAutomationOptions
{
    // Timeouts
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ElementWaitPollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan SessionAcquireTimeout { get; set; } = TimeSpan.FromSeconds(15);
    
    // Caching
    public bool EnableCaching { get; set; } = true;
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(5);
    
    // Tree walking
    public int MaxTreeDepth { get; set; } = 50;
    public bool UseControlView { get; set; } = true;
    
    // Actions
    public int DefaultClickDelay { get; set; } = 50;
    public int DefaultTypeDelay { get; set; } = 20;
    public VirtualInputProfile InputProfile { get; set; } = VirtualInputProfile.Balanced;
    
    // Retry
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    
    // Session reuse
    public bool AllowSessionReuse { get; set; } = true;
}

public enum VirtualInputProfile
{
    LowLatency,
    Balanced,
    HighThroughput
}
```

## Error Handling

```csharp
public class UIAutomationException : Exception
{
    public string? ElementId { get; }
    public UIAutomationErrorCode ErrorCode { get; }
}

public enum UIAutomationErrorCode
{
    ElementNotFound,
    ElementNotEnabled,
    ElementNotVisible,
    PatternNotSupported,
    ActionFailed,
    Timeout,
    ProcessNotFound,
    WindowNotFound,
    InvalidOperation,
    SessionUnavailable,
    SessionExpired
}
```

## Usage Examples

### Acquiring a session-scoped UIAutomation client
```csharp
// sessionService comes from Cascade.Grpc.SessionService
var sessionHandle = await sessionService.AttachAsync(new AttachSessionRequest
{
    AgentId = agentId,
    PreferredResolution = new Resolution { Width = 1920, Height = 1080 }
});

var sessionContext = new SessionContext(sessionHandle);
var discovery = new ElementDiscovery(sessionContext);

// All subsequent calls run inside the hidden desktop and use virtual input
var calcWindow = await discovery.WaitForElementAsync(
    SearchCriteria.ByName("Calculator"),
    TimeSpan.FromSeconds(5));
```

### Finding and clicking a button
```csharp
var discovery = new ElementDiscovery();
var window = await discovery.WaitForElementAsync(
    SearchCriteria.ByName("Calculator"),
    TimeSpan.FromSeconds(10));

var button = window.FindFirst(
    SearchCriteria.ByAutomationId("num1Button"));

await button.ClickAsync();
```

### Walking the UI tree
```csharp
var walker = new UITreeWalker();
var snapshot = walker.CaptureSnapshot(window, maxDepth: 5);

foreach (var element in snapshot.Root.Children)
{
    Console.WriteLine($"{element.ControlType}: {element.Name}");
}
```

### Using patterns
```csharp
var textBox = window.FindFirst(SearchCriteria.ByControlType(ControlType.Edit));

if (textBox.TryGetPattern<IValuePattern>(out var valuePattern))
{
    await valuePattern.SetValueAsync("Hello, World!");
}
```

## Performance Considerations

1. **Use caching**: Element lookups are expensive; cache frequently accessed elements
2. **Limit tree depth**: Deep tree walks can be slow; specify max depth when possible
3. **Use Control View**: Faster than Raw View, excludes non-interactive elements
4. **Batch operations**: Group related operations to minimize round-trips
5. **Prefer AutomationId**: Faster than name-based searches when available

## Session Routing & Safety

- **One Service per Session**: Services are created via `IServiceProvider.GetRequiredService<UIAutomationService>(SessionHandle)` so there is no accidental cross-session state bleed.
- **Virtual Input Backpressure**: `IVirtualInputProvider` enforces rate limits to keep the hidden desktop responsive and prevents starved user sessions.
- **Session Failover**: When the virtual desktop crashes, the `SessionRouter` signals `SessionExpired`, allowing agents to reacquire a fresh handle without user intervention.
- **User Concurrency**: Because all input/output is virtualized, the end user can continue working on the foreground desktop while automation proceeds in the background.


