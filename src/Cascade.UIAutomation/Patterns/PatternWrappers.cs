using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;
using FlaUI.Core.AutomationElements;
using ExpandCollapseState = Cascade.UIAutomation.Enums.ExpandCollapseState;
using ToggleState = Cascade.UIAutomation.Enums.ToggleState;

namespace Cascade.UIAutomation.Patterns;

#region FlaUI Pattern Wrappers

/// <summary>
/// Wrapper for the FlaUI Invoke pattern.
/// </summary>
internal class FlaUIInvokePatternWrapper : IInvokePattern
{
    private readonly FlaUI.Core.Patterns.IInvokePattern _pattern;

    public FlaUIInvokePatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.IInvokePattern)pattern;
    }

    public Task InvokeAsync()
    {
        return Task.Run(() => _pattern.Invoke());
    }
}

/// <summary>
/// Wrapper for the FlaUI Value pattern.
/// </summary>
internal class FlaUIValuePatternWrapper : IValuePattern
{
    private readonly FlaUI.Core.Patterns.IValuePattern _pattern;

    public FlaUIValuePatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.IValuePattern)pattern;
    }

    public string Value
    {
        get
        {
            try
            {
                return _pattern.Value.ValueOrDefault ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public bool IsReadOnly
    {
        get
        {
            try
            {
                return _pattern.IsReadOnly.ValueOrDefault;
            }
            catch
            {
                return true;
            }
        }
    }

    public Task SetValueAsync(string value)
    {
        return Task.Run(() => _pattern.SetValue(value));
    }
}

/// <summary>
/// Wrapper for the FlaUI Toggle pattern.
/// </summary>
internal class FlaUITogglePatternWrapper : ITogglePattern
{
    private readonly FlaUI.Core.Patterns.ITogglePattern _pattern;

    public FlaUITogglePatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.ITogglePattern)pattern;
    }

    public ToggleState ToggleState
    {
        get
        {
            try
            {
                var state = _pattern.ToggleState.ValueOrDefault;
                return (ToggleState)(int)state;
            }
            catch
            {
                return ToggleState.Off;
            }
        }
    }

    public Task ToggleAsync()
    {
        return Task.Run(() => _pattern.Toggle());
    }
}

/// <summary>
/// Wrapper for the FlaUI Selection pattern.
/// </summary>
internal class FlaUISelectionPatternWrapper : ISelectionPattern
{
    private readonly FlaUI.Core.Patterns.ISelectionPattern _pattern;
    private readonly ElementFactory _factory;

    public FlaUISelectionPatternWrapper(object pattern, ElementFactory factory)
    {
        _pattern = (FlaUI.Core.Patterns.ISelectionPattern)pattern;
        _factory = factory;
    }

    public IReadOnlyList<IUIElement> GetSelection()
    {
        try
        {
            var selection = _pattern.Selection.ValueOrDefault;
            if (selection == null)
                return Array.Empty<IUIElement>();

            return selection
                .Select(e => _factory.Create(e))
                .Where(e => e != null)
                .Cast<IUIElement>()
                .ToList();
        }
        catch
        {
            return Array.Empty<IUIElement>();
        }
    }

    public bool CanSelectMultiple
    {
        get
        {
            try
            {
                return _pattern.CanSelectMultiple.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsSelectionRequired
    {
        get
        {
            try
            {
                return _pattern.IsSelectionRequired.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }
}

/// <summary>
/// Wrapper for the FlaUI SelectionItem pattern.
/// </summary>
internal class FlaUISelectionItemPatternWrapper : ISelectionItemPattern
{
    private readonly FlaUI.Core.Patterns.ISelectionItemPattern _pattern;
    private readonly ElementFactory _factory;

    public FlaUISelectionItemPatternWrapper(object pattern, ElementFactory factory)
    {
        _pattern = (FlaUI.Core.Patterns.ISelectionItemPattern)pattern;
        _factory = factory;
    }

    public bool IsSelected
    {
        get
        {
            try
            {
                return _pattern.IsSelected.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public IUIElement? SelectionContainer
    {
        get
        {
            try
            {
                var container = _pattern.SelectionContainer.ValueOrDefault;
                return _factory.Create(container);
            }
            catch
            {
                return null;
            }
        }
    }

    public Task SelectAsync()
    {
        return Task.Run(() => _pattern.Select());
    }

    public Task AddToSelectionAsync()
    {
        return Task.Run(() => _pattern.AddToSelection());
    }

    public Task RemoveFromSelectionAsync()
    {
        return Task.Run(() => _pattern.RemoveFromSelection());
    }
}

/// <summary>
/// Wrapper for the FlaUI ExpandCollapse pattern.
/// </summary>
internal class FlaUIExpandCollapsePatternWrapper : IExpandCollapsePattern
{
    private readonly FlaUI.Core.Patterns.IExpandCollapsePattern _pattern;

    public FlaUIExpandCollapsePatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.IExpandCollapsePattern)pattern;
    }

    public ExpandCollapseState State
    {
        get
        {
            try
            {
                var state = _pattern.ExpandCollapseState.ValueOrDefault;
                return (ExpandCollapseState)(int)state;
            }
            catch
            {
                return ExpandCollapseState.LeafNode;
            }
        }
    }

    public Task ExpandAsync()
    {
        return Task.Run(() => _pattern.Expand());
    }

    public Task CollapseAsync()
    {
        return Task.Run(() => _pattern.Collapse());
    }
}

/// <summary>
/// Wrapper for the FlaUI Scroll pattern.
/// </summary>
internal class FlaUIScrollPatternWrapper : IScrollPattern
{
    private readonly FlaUI.Core.Patterns.IScrollPattern _pattern;

    public FlaUIScrollPatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.IScrollPattern)pattern;
    }

    public double HorizontalScrollPercent
    {
        get
        {
            try
            {
                return _pattern.HorizontalScrollPercent.ValueOrDefault;
            }
            catch
            {
                return 0;
            }
        }
    }

    public double VerticalScrollPercent
    {
        get
        {
            try
            {
                return _pattern.VerticalScrollPercent.ValueOrDefault;
            }
            catch
            {
                return 0;
            }
        }
    }

    public double HorizontalViewSize
    {
        get
        {
            try
            {
                return _pattern.HorizontalViewSize.ValueOrDefault;
            }
            catch
            {
                return 100;
            }
        }
    }

    public double VerticalViewSize
    {
        get
        {
            try
            {
                return _pattern.VerticalViewSize.ValueOrDefault;
            }
            catch
            {
                return 100;
            }
        }
    }

    public bool HorizontallyScrollable
    {
        get
        {
            try
            {
                return _pattern.HorizontallyScrollable.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool VerticallyScrollable
    {
        get
        {
            try
            {
                return _pattern.VerticallyScrollable.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public Task ScrollAsync(ScrollAmount horizontal, ScrollAmount vertical)
    {
        return Task.Run(() => _pattern.Scroll(
            (FlaUI.Core.Definitions.ScrollAmount)(int)horizontal,
            (FlaUI.Core.Definitions.ScrollAmount)(int)vertical));
    }

    public Task SetScrollPercentAsync(double horizontal, double vertical)
    {
        return Task.Run(() => _pattern.SetScrollPercent(horizontal, vertical));
    }
}

/// <summary>
/// Wrapper for the FlaUI ScrollItem pattern.
/// </summary>
internal class FlaUIScrollItemPatternWrapper : IScrollItemPattern
{
    private readonly FlaUI.Core.Patterns.IScrollItemPattern _pattern;

    public FlaUIScrollItemPatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.IScrollItemPattern)pattern;
    }

    public Task ScrollIntoViewAsync()
    {
        return Task.Run(() => _pattern.ScrollIntoView());
    }
}

/// <summary>
/// Wrapper for the FlaUI RangeValue pattern.
/// </summary>
internal class FlaUIRangeValuePatternWrapper : IRangeValuePattern
{
    private readonly FlaUI.Core.Patterns.IRangeValuePattern _pattern;

    public FlaUIRangeValuePatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.IRangeValuePattern)pattern;
    }

    public double Value
    {
        get
        {
            try
            {
                return _pattern.Value.ValueOrDefault;
            }
            catch
            {
                return 0;
            }
        }
    }

    public bool IsReadOnly
    {
        get
        {
            try
            {
                return _pattern.IsReadOnly.ValueOrDefault;
            }
            catch
            {
                return true;
            }
        }
    }

    public double Maximum
    {
        get
        {
            try
            {
                return _pattern.Maximum.ValueOrDefault;
            }
            catch
            {
                return 0;
            }
        }
    }

    public double Minimum
    {
        get
        {
            try
            {
                return _pattern.Minimum.ValueOrDefault;
            }
            catch
            {
                return 0;
            }
        }
    }

    public double SmallChange
    {
        get
        {
            try
            {
                return _pattern.SmallChange.ValueOrDefault;
            }
            catch
            {
                return 1;
            }
        }
    }

    public double LargeChange
    {
        get
        {
            try
            {
                return _pattern.LargeChange.ValueOrDefault;
            }
            catch
            {
                return 10;
            }
        }
    }

    public Task SetValueAsync(double value)
    {
        return Task.Run(() => _pattern.SetValue(value));
    }
}

/// <summary>
/// Wrapper for the FlaUI Window pattern.
/// </summary>
internal class FlaUIWindowPatternWrapper : IWindowPattern
{
    private readonly FlaUI.Core.Patterns.IWindowPattern _pattern;

    public FlaUIWindowPatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.IWindowPattern)pattern;
    }

    public bool CanMaximize
    {
        get
        {
            try
            {
                return _pattern.CanMaximize.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanMinimize
    {
        get
        {
            try
            {
                return _pattern.CanMinimize.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsModal
    {
        get
        {
            try
            {
                return _pattern.IsModal.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsTopmost
    {
        get
        {
            try
            {
                return _pattern.IsTopmost.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public WindowVisualState WindowVisualState
    {
        get
        {
            try
            {
                var state = _pattern.WindowVisualState.ValueOrDefault;
                return (WindowVisualState)(int)state;
            }
            catch
            {
                return WindowVisualState.Normal;
            }
        }
    }

    public WindowInteractionState WindowInteractionState
    {
        get
        {
            try
            {
                var state = _pattern.WindowInteractionState.ValueOrDefault;
                return (WindowInteractionState)(int)state;
            }
            catch
            {
                return WindowInteractionState.Running;
            }
        }
    }

    public Task SetWindowVisualStateAsync(WindowVisualState state)
    {
        return Task.Run(() => _pattern.SetWindowVisualState(
            (FlaUI.Core.Definitions.WindowVisualState)(int)state));
    }

    public Task CloseAsync()
    {
        return Task.Run(() => _pattern.Close());
    }

    public Task<bool> WaitForInputIdleAsync(int milliseconds)
    {
        return Task.Run(() => _pattern.WaitForInputIdle(milliseconds));
    }
}

/// <summary>
/// Wrapper for the FlaUI Transform pattern.
/// </summary>
internal class FlaUITransformPatternWrapper : ITransformPattern
{
    private readonly FlaUI.Core.Patterns.ITransformPattern _pattern;

    public FlaUITransformPatternWrapper(object pattern)
    {
        _pattern = (FlaUI.Core.Patterns.ITransformPattern)pattern;
    }

    public bool CanMove
    {
        get
        {
            try
            {
                return _pattern.CanMove.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanResize
    {
        get
        {
            try
            {
                return _pattern.CanResize.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanRotate
    {
        get
        {
            try
            {
                return _pattern.CanRotate.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }
    }

    public Task MoveAsync(double x, double y)
    {
        return Task.Run(() => _pattern.Move(x, y));
    }

    public Task ResizeAsync(double width, double height)
    {
        return Task.Run(() => _pattern.Resize(width, height));
    }

    public Task RotateAsync(double degrees)
    {
        return Task.Run(() => _pattern.Rotate(degrees));
    }
}

#endregion
