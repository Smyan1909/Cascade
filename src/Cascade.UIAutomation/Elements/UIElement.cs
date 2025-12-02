using System.Drawing;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Enums;
using Cascade.UIAutomation.Exceptions;
using Cascade.UIAutomation.Interop;
using Cascade.UIAutomation.Patterns;
using FlaUI.Core.AutomationElements;
using ControlType = Cascade.UIAutomation.Enums.ControlType;
using TreeScope = Cascade.UIAutomation.Interop.TreeScope;

namespace Cascade.UIAutomation.Elements;

/// <summary>
/// Implementation of IUIElement wrapping a FlaUI AutomationElement.
/// </summary>
public class UIElement : IUIElement
{
    private readonly AutomationElement _nativeElement;
    private readonly ElementFactory _factory;
    private readonly IUIAutomationWrapper _automation;
    private IReadOnlyList<PatternType>? _supportedPatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="UIElement"/> class.
    /// </summary>
    internal UIElement(object nativeElement, ElementFactory factory, IUIAutomationWrapper automation)
    {
        _nativeElement = (AutomationElement)nativeElement;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));
    }

    /// <summary>
    /// Gets the underlying native element.
    /// </summary>
    internal AutomationElement NativeElement => _nativeElement;

    #region Identity Properties

    /// <inheritdoc />
    public string AutomationId
    {
        get
        {
            try
            {
                return _nativeElement.AutomationId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <inheritdoc />
    public string Name
    {
        get
        {
            try
            {
                return _nativeElement.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <inheritdoc />
    public string ClassName
    {
        get
        {
            try
            {
                return _nativeElement.ClassName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <inheritdoc />
    public ControlType ControlType
    {
        get
        {
            try
            {
                var flaControlType = _nativeElement.ControlType;
                return MapFromFlaUIControlType(flaControlType);
            }
            catch
            {
                return ControlType.Unknown;
            }
        }
    }

    /// <inheritdoc />
    public string RuntimeId
    {
        get
        {
            try
            {
                var ids = _nativeElement.Properties.RuntimeId.ValueOrDefault;
                return ids != null ? string.Join(".", ids) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <inheritdoc />
    public int ProcessId
    {
        get
        {
            try
            {
                return _nativeElement.Properties.ProcessId.ValueOrDefault;
            }
            catch
            {
                return 0;
            }
        }
    }

    #endregion

    #region Hierarchy

    /// <inheritdoc />
    public IUIElement? Parent
    {
        get
        {
            var walker = _automation.CreateControlViewWalker();
            var parent = _automation.GetParent(_nativeElement, walker);
            return _factory.Create(parent);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IUIElement> Children
    {
        get
        {
            var condition = _automation.CreateTrueCondition();
            var results = _automation.FindAll(_nativeElement, TreeScope.Children, condition);
            return results.Select(e => _factory.Create(e)!).ToList();
        }
    }

    /// <inheritdoc />
    public IUIElement? FindFirst(SearchCriteria criteria)
    {
        var condition = _automation.CreateCondition(criteria);
        var result = _automation.FindFirst(_nativeElement, TreeScope.Descendants, condition);
        return _factory.Create(result);
    }

    /// <inheritdoc />
    public IReadOnlyList<IUIElement> FindAll(SearchCriteria criteria)
    {
        var condition = _automation.CreateCondition(criteria);
        var results = _automation.FindAll(_nativeElement, TreeScope.Descendants, condition);
        return results.Select(e => _factory.Create(e)!).ToList();
    }

    #endregion

    #region Geometry

    /// <inheritdoc />
    public Rectangle BoundingRectangle
    {
        get
        {
            try
            {
                var rect = _nativeElement.BoundingRectangle;
                return new Rectangle(
                    (int)rect.X,
                    (int)rect.Y,
                    (int)rect.Width,
                    (int)rect.Height);
            }
            catch
            {
                return Rectangle.Empty;
            }
        }
    }

    /// <inheritdoc />
    public Point ClickablePoint
    {
        get
        {
            try
            {
                var point = _nativeElement.GetClickablePoint();
                return new Point((int)point.X, (int)point.Y);
            }
            catch
            {
                var rect = BoundingRectangle;
                return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            }
        }
    }

    /// <inheritdoc />
    public bool IsOffscreen
    {
        get
        {
            try
            {
                return _nativeElement.IsOffscreen;
            }
            catch
            {
                return true;
            }
        }
    }

    #endregion

    #region State

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            try
            {
                return _nativeElement.IsEnabled;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public bool HasKeyboardFocus
    {
        get
        {
            try
            {
                return _nativeElement.Properties.HasKeyboardFocus.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public bool IsContentElement
    {
        get
        {
            try
            {
                return _nativeElement.Properties.IsContentElement.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public bool IsControlElement
    {
        get
        {
            try
            {
                return _nativeElement.Properties.IsControlElement.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    #endregion

    #region Patterns

    /// <inheritdoc />
    public bool TryGetPattern<T>(out T? pattern) where T : class
    {
        pattern = default;

        var patternId = GetPatternIdForType<T>();
        if (patternId == 0)
            return false;

        var nativePattern = _automation.GetPattern(_nativeElement, patternId);
        if (nativePattern == null)
            return false;

        pattern = CreatePatternWrapper<T>(nativePattern);
        return pattern != null;
    }

    /// <inheritdoc />
    public IReadOnlyList<PatternType> SupportedPatterns
    {
        get
        {
            if (_supportedPatterns != null)
                return _supportedPatterns;

            var patterns = new List<PatternType>();
            foreach (PatternType pt in Enum.GetValues<PatternType>())
            {
                try
                {
                    var nativePattern = _automation.GetPattern(_nativeElement, (int)pt);
                    if (nativePattern != null)
                        patterns.Add(pt);
                }
                catch
                {
                    // Pattern not supported
                }
            }

            _supportedPatterns = patterns;
            return patterns;
        }
    }

    private static int GetPatternIdForType<T>()
    {
        return typeof(T).Name switch
        {
            nameof(IInvokePattern) => UIA_PatternIds.UIA_InvokePatternId,
            nameof(IValuePattern) => UIA_PatternIds.UIA_ValuePatternId,
            nameof(ITogglePattern) => UIA_PatternIds.UIA_TogglePatternId,
            nameof(ISelectionPattern) => UIA_PatternIds.UIA_SelectionPatternId,
            nameof(ISelectionItemPattern) => UIA_PatternIds.UIA_SelectionItemPatternId,
            nameof(IExpandCollapsePattern) => UIA_PatternIds.UIA_ExpandCollapsePatternId,
            nameof(IScrollPattern) => UIA_PatternIds.UIA_ScrollPatternId,
            nameof(IScrollItemPattern) => UIA_PatternIds.UIA_ScrollItemPatternId,
            nameof(IRangeValuePattern) => UIA_PatternIds.UIA_RangeValuePatternId,
            nameof(IWindowPattern) => UIA_PatternIds.UIA_WindowPatternId,
            nameof(ITransformPattern) => UIA_PatternIds.UIA_TransformPatternId,
            _ => 0
        };
    }

    private T? CreatePatternWrapper<T>(object nativePattern) where T : class
    {
        return typeof(T).Name switch
        {
            nameof(IInvokePattern) => new FlaUIInvokePatternWrapper(nativePattern) as T,
            nameof(IValuePattern) => new FlaUIValuePatternWrapper(nativePattern) as T,
            nameof(ITogglePattern) => new FlaUITogglePatternWrapper(nativePattern) as T,
            nameof(ISelectionPattern) => new FlaUISelectionPatternWrapper(nativePattern, _factory) as T,
            nameof(ISelectionItemPattern) => new FlaUISelectionItemPatternWrapper(nativePattern, _factory) as T,
            nameof(IExpandCollapsePattern) => new FlaUIExpandCollapsePatternWrapper(nativePattern) as T,
            nameof(IScrollPattern) => new FlaUIScrollPatternWrapper(nativePattern) as T,
            nameof(IScrollItemPattern) => new FlaUIScrollItemPatternWrapper(nativePattern) as T,
            nameof(IRangeValuePattern) => new FlaUIRangeValuePatternWrapper(nativePattern) as T,
            nameof(IWindowPattern) => new FlaUIWindowPatternWrapper(nativePattern) as T,
            nameof(ITransformPattern) => new FlaUITransformPatternWrapper(nativePattern) as T,
            _ => null
        };
    }

    #endregion

    #region Actions

    /// <inheritdoc />
    public Task ClickAsync(ClickType clickType = ClickType.Left)
    {
        return Task.Run(() =>
        {
            EnsureEnabled();
            var point = ClickablePoint;

            switch (clickType)
            {
                case ClickType.Left:
                    InputSimulator.LeftClick(point.X, point.Y);
                    break;
                case ClickType.Right:
                    InputSimulator.RightClick(point.X, point.Y);
                    break;
                case ClickType.Middle:
                    InputSimulator.MiddleClick(point.X, point.Y);
                    break;
                case ClickType.Double:
                    InputSimulator.DoubleClick(point.X, point.Y);
                    break;
            }
        });
    }

    /// <inheritdoc />
    public Task DoubleClickAsync()
    {
        return ClickAsync(ClickType.Double);
    }

    /// <inheritdoc />
    public Task RightClickAsync()
    {
        return ClickAsync(ClickType.Right);
    }

    /// <inheritdoc />
    public Task TypeTextAsync(string text)
    {
        return Task.Run(async () =>
        {
            EnsureEnabled();
            await SetFocusAsync();
            await Task.Delay(50); // Brief delay after focus
            InputSimulator.TypeText(text);
        });
    }

    /// <inheritdoc />
    public Task SetValueAsync(string value)
    {
        return Task.Run(() =>
        {
            EnsureEnabled();

            if (TryGetPattern<IValuePattern>(out var valuePattern) && valuePattern != null)
            {
                if (valuePattern.IsReadOnly)
                    throw UIAutomationException.InvalidOperation("Element value is read-only");

                valuePattern.SetValueAsync(value).Wait();
            }
            else
            {
                throw UIAutomationException.PatternNotSupported(nameof(IValuePattern), RuntimeId);
            }
        });
    }

    /// <inheritdoc />
    public Task InvokeAsync()
    {
        return Task.Run(() =>
        {
            EnsureEnabled();

            if (TryGetPattern<IInvokePattern>(out var invokePattern) && invokePattern != null)
            {
                invokePattern.InvokeAsync().Wait();
            }
            else
            {
                // Fall back to click
                ClickAsync().Wait();
            }
        });
    }

    /// <inheritdoc />
    public Task SetFocusAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _nativeElement.Focus();
            }
            catch (Exception ex)
            {
                throw UIAutomationException.ActionFailed("SetFocus", RuntimeId, ex);
            }
        });
    }

    /// <inheritdoc />
    public Task ScrollIntoViewAsync()
    {
        return Task.Run(() =>
        {
            if (TryGetPattern<IScrollItemPattern>(out var scrollItemPattern) && scrollItemPattern != null)
            {
                scrollItemPattern.ScrollIntoViewAsync().Wait();
            }
        });
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
            throw UIAutomationException.ElementNotEnabled(RuntimeId);
    }

    #endregion

    #region Serialization

    /// <inheritdoc />
    public ElementSnapshot ToSnapshot()
    {
        var snapshot = new ElementSnapshot
        {
            RuntimeId = RuntimeId,
            AutomationId = AutomationId,
            Name = Name,
            ClassName = ClassName,
            ControlType = ControlType.ToString(),
            ControlTypeId = (int)ControlType,
            BoundingRectangle = BoundingRectangle,
            IsEnabled = IsEnabled,
            IsOffscreen = IsOffscreen,
            IsContentElement = IsContentElement,
            IsControlElement = IsControlElement,
            HasKeyboardFocus = HasKeyboardFocus,
            ProcessId = ProcessId,
            SupportedPatterns = SupportedPatterns.Select(p => p.ToString()).ToList()
        };

        // Try to get value if ValuePattern is supported
        if (TryGetPattern<IValuePattern>(out var valuePattern) && valuePattern != null)
        {
            snapshot.Value = valuePattern.Value;
        }

        return snapshot;
    }

    #endregion

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{ControlType}: {Name} [{AutomationId}]";
    }

    private static ControlType MapFromFlaUIControlType(FlaUI.Core.Definitions.ControlType flaControlType)
    {
        return flaControlType switch
        {
            FlaUI.Core.Definitions.ControlType.Button => ControlType.Button,
            FlaUI.Core.Definitions.ControlType.Calendar => ControlType.Calendar,
            FlaUI.Core.Definitions.ControlType.CheckBox => ControlType.CheckBox,
            FlaUI.Core.Definitions.ControlType.ComboBox => ControlType.ComboBox,
            FlaUI.Core.Definitions.ControlType.Edit => ControlType.Edit,
            FlaUI.Core.Definitions.ControlType.Hyperlink => ControlType.Hyperlink,
            FlaUI.Core.Definitions.ControlType.Image => ControlType.Image,
            FlaUI.Core.Definitions.ControlType.ListItem => ControlType.ListItem,
            FlaUI.Core.Definitions.ControlType.List => ControlType.List,
            FlaUI.Core.Definitions.ControlType.Menu => ControlType.Menu,
            FlaUI.Core.Definitions.ControlType.MenuBar => ControlType.MenuBar,
            FlaUI.Core.Definitions.ControlType.MenuItem => ControlType.MenuItem,
            FlaUI.Core.Definitions.ControlType.ProgressBar => ControlType.ProgressBar,
            FlaUI.Core.Definitions.ControlType.RadioButton => ControlType.RadioButton,
            FlaUI.Core.Definitions.ControlType.ScrollBar => ControlType.ScrollBar,
            FlaUI.Core.Definitions.ControlType.Slider => ControlType.Slider,
            FlaUI.Core.Definitions.ControlType.Spinner => ControlType.Spinner,
            FlaUI.Core.Definitions.ControlType.StatusBar => ControlType.StatusBar,
            FlaUI.Core.Definitions.ControlType.Tab => ControlType.Tab,
            FlaUI.Core.Definitions.ControlType.TabItem => ControlType.TabItem,
            FlaUI.Core.Definitions.ControlType.Text => ControlType.Text,
            FlaUI.Core.Definitions.ControlType.ToolBar => ControlType.ToolBar,
            FlaUI.Core.Definitions.ControlType.ToolTip => ControlType.ToolTip,
            FlaUI.Core.Definitions.ControlType.Tree => ControlType.Tree,
            FlaUI.Core.Definitions.ControlType.TreeItem => ControlType.TreeItem,
            FlaUI.Core.Definitions.ControlType.Custom => ControlType.Custom,
            FlaUI.Core.Definitions.ControlType.Group => ControlType.Group,
            FlaUI.Core.Definitions.ControlType.Thumb => ControlType.Thumb,
            FlaUI.Core.Definitions.ControlType.DataGrid => ControlType.DataGrid,
            FlaUI.Core.Definitions.ControlType.DataItem => ControlType.DataItem,
            FlaUI.Core.Definitions.ControlType.Document => ControlType.Document,
            FlaUI.Core.Definitions.ControlType.SplitButton => ControlType.SplitButton,
            FlaUI.Core.Definitions.ControlType.Window => ControlType.Window,
            FlaUI.Core.Definitions.ControlType.Pane => ControlType.Pane,
            FlaUI.Core.Definitions.ControlType.Header => ControlType.Header,
            FlaUI.Core.Definitions.ControlType.HeaderItem => ControlType.HeaderItem,
            FlaUI.Core.Definitions.ControlType.Table => ControlType.Table,
            FlaUI.Core.Definitions.ControlType.TitleBar => ControlType.TitleBar,
            FlaUI.Core.Definitions.ControlType.Separator => ControlType.Separator,
            _ => ControlType.Unknown
        };
    }
}
