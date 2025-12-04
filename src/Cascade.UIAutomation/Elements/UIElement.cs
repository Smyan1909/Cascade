using Cascade.UIAutomation.Actions;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Input;
using Cascade.UIAutomation.Models;
using Cascade.UIAutomation.Patterns;
using Cascade.UIAutomation.Session;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Windows.Automation;
using NativeTreeWalker = System.Windows.Automation.TreeWalker;

namespace Cascade.UIAutomation.Elements;

public sealed class UIElement : IUIElement
{
    private readonly AutomationElement _element;
    private readonly SessionContext _context;
    private readonly IVirtualInputProvider _inputProvider;
    private readonly ElementFactory _factory;
    private readonly ILogger? _logger;
    private readonly Lazy<IReadOnlyList<IUIElement>> _children;

    internal UIElement(
        AutomationElement element,
        SessionContext context,
        IVirtualInputProvider inputProvider,
        ElementFactory factory,
        ILogger? logger)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger;

        _children = new Lazy<IReadOnlyList<IUIElement>>(LoadChildren);
    }

    internal AutomationElement AutomationElement => _element;

    public SessionHandle Session => _context.Session;
    public VirtualInputChannel InputChannel => _context.InputChannel;

    public string AutomationId => _element.Current.AutomationId ?? string.Empty;
    public string Name => _element.Current.Name ?? string.Empty;
    public string ClassName => _element.Current.ClassName ?? string.Empty;
    public ControlType ControlType => _element.Current.ControlType;
    public string RuntimeId => string.Join(".", _element.GetRuntimeId() ?? Array.Empty<int>());

    public IUIElement? Parent
    {
        get
        {
            var walker = NativeTreeWalker.ControlViewWalker;
            var parentElement = walker.GetParent(_element);
            return parentElement is null ? null : _factory.Create(parentElement);
        }
    }

    public IReadOnlyList<IUIElement> Children => _children.Value;
    public int ProcessId => _element.Current.ProcessId;

    public Rectangle BoundingRectangle => Rectangle.FromLTRB(
        (int)_element.Current.BoundingRectangle.Left,
        (int)_element.Current.BoundingRectangle.Top,
        (int)_element.Current.BoundingRectangle.Right,
        (int)_element.Current.BoundingRectangle.Bottom);

    public Point ClickablePoint
    {
        get
        {
            try
            {
                var point = _element.GetClickablePoint();
                return new Point((int)point.X, (int)point.Y);
            }
            catch (NoClickablePointException)
            {
                var rect = BoundingRectangle;
                return new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            }
        }
    }

    public bool IsOffscreen => _element.Current.IsOffscreen;
    public bool IsEnabled => _element.Current.IsEnabled;
    public bool HasKeyboardFocus => _element.Current.HasKeyboardFocus;
    public bool IsContentElement => _element.Current.IsContentElement;
    public bool IsControlElement => _element.Current.IsControlElement;

    public IReadOnlyList<PatternType> SupportedPatterns
    {
        get
        {
            var patterns = _element.GetSupportedPatterns();
            return patterns.Select(MapPattern).Where(p => p is not null).Select(p => p!.Value).ToList();
        }
    }

    public IUIElement? FindFirst(SearchCriteria criteria)
    {
        var native = _element.FindFirst(TreeScope.Subtree, criteria?.ToAutomationCondition() ?? Condition.TrueCondition);
        return native is null ? null : _factory.Create(native);
    }

    public IReadOnlyList<IUIElement> FindAll(SearchCriteria criteria)
    {
        var results = _element.FindAll(TreeScope.Subtree, criteria?.ToAutomationCondition() ?? Condition.TrueCondition);
        return _factory.CreateMany(results);
    }

    public async Task ClickAsync(ClickType clickType = ClickType.Left)
    {
        EnsureEnabled();
        var button = clickType switch
        {
            ClickType.Left => MouseButton.Left,
            ClickType.Right => MouseButton.Right,
            ClickType.Middle => MouseButton.Middle,
            _ => MouseButton.Left
        };

        await _inputProvider.MoveMouseAsync(ClickablePoint).ConfigureAwait(false);
        await _inputProvider.ClickAsync(button).ConfigureAwait(false);
    }

    public Task DoubleClickAsync()
    {
        return _inputProvider.ClickAsync(MouseButton.Left, new ClickOptions { ClickCount = 2 });
    }

    public Task RightClickAsync()
    {
        return ClickAsync(ClickType.Right);
    }

    public async Task TypeTextAsync(string text)
    {
        EnsureEnabled();
        await SetFocusAsync().ConfigureAwait(false);
        await _inputProvider.TypeTextAsync(text).ConfigureAwait(false);
    }

    public async Task SetValueAsync(string value)
    {
        if (TryGetPattern<IValuePattern>(out var valuePattern))
        {
            await valuePattern.SetValueAsync(value).ConfigureAwait(false);
            return;
        }

        await TypeTextAsync(value).ConfigureAwait(false);
    }

    public async Task InvokeAsync()
    {
        if (TryGetPattern<IInvokePattern>(out var pattern))
        {
            await pattern.InvokeAsync().ConfigureAwait(false);
            return;
        }

        await ClickAsync().ConfigureAwait(false);
    }

    public Task SetFocusAsync()
    {
        _element.SetFocus();
        return Task.CompletedTask;
    }

    public bool TryGetPattern<T>(out T pattern) where T : class
    {
        pattern = default!;
        var type = typeof(T);

        if (type == typeof(IInvokePattern) && _element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
        {
            pattern = (T)(object)new InvokePatternAdapter((InvokePattern)invoke);
            return true;
        }

        if (type == typeof(IValuePattern) && _element.TryGetCurrentPattern(ValuePattern.Pattern, out var value))
        {
            pattern = (T)(object)new ValuePatternAdapter((ValuePattern)value);
            return true;
        }

        if (type == typeof(ISelectionPattern) && _element.TryGetCurrentPattern(SelectionPattern.Pattern, out var selection))
        {
            pattern = (T)(object)new SelectionPatternAdapter((SelectionPattern)selection, _factory);
            return true;
        }

        if (type == typeof(ISelectionItemPattern) && _element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionItem))
        {
            pattern = (T)(object)new SelectionItemPatternAdapter((SelectionItemPattern)selectionItem, _factory);
            return true;
        }

        if (type == typeof(ITogglePattern) && _element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle))
        {
            pattern = (T)(object)new TogglePatternAdapter((TogglePattern)toggle);
            return true;
        }

        if (type == typeof(IExpandCollapsePattern) && _element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandCollapse))
        {
            pattern = (T)(object)new ExpandCollapsePatternAdapter((ExpandCollapsePattern)expandCollapse);
            return true;
        }

        if (type == typeof(IScrollPattern) && _element.TryGetCurrentPattern(ScrollPattern.Pattern, out var scroll))
        {
            pattern = (T)(object)new ScrollPatternAdapter((ScrollPattern)scroll);
            return true;
        }

        return false;
    }

    public ElementSnapshot ToSnapshot()
    {
        return new ElementSnapshot
        {
            RuntimeId = RuntimeId,
            AutomationId = AutomationId,
            Name = Name,
            ClassName = ClassName,
            ControlType = ControlType.ProgrammaticName,
            BoundingRectangle = BoundingRectangle,
            IsEnabled = IsEnabled,
            IsOffscreen = IsOffscreen,
            SupportedPatterns = SupportedPatterns.Select(p => p.ToString()).ToList(),
            Children = Children.Select(child => child.ToSnapshot()).ToList()
        };
    }

    private IReadOnlyList<IUIElement> LoadChildren()
    {
        var results = _element.FindAll(TreeScope.Children, Condition.TrueCondition);
        return _factory.CreateMany(results);
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException($"Element '{Name}' is not enabled.");
        }
    }

    private static PatternType? MapPattern(AutomationPattern pattern)
    {
        if (pattern == InvokePattern.Pattern) return PatternType.Invoke;
        if (pattern == ValuePattern.Pattern) return PatternType.Value;
        if (pattern == SelectionPattern.Pattern) return PatternType.Selection;
        if (pattern == SelectionItemPattern.Pattern) return PatternType.SelectionItem;
        if (pattern == TogglePattern.Pattern) return PatternType.Toggle;
        if (pattern == ExpandCollapsePattern.Pattern) return PatternType.ExpandCollapse;
        if (pattern == ScrollPattern.Pattern) return PatternType.Scroll;
        return null;
    }
}


