using Cascade.Proto;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Patterns;
using FlaUI.Core.Definitions;
using FlaUI.Core;
using FlaUI.Core.Patterns.Infrastructure;

namespace Cascade.Body.Automation;

public static class UiaPatterns
{
    public static bool TryInvoke(AutomationElement element)
    {
        try
        {
            var pattern = element.Patterns.Invoke.PatternOrDefault;
            if (pattern == null) return false;
            pattern.Invoke();
            return true;
        }
        catch
        {
            // Invoke can throw on certain elements
            return false;
        }
    }

    public static bool TrySetValue(AutomationElement element, string? text)
    {
        try
        {
            var pattern = element.Patterns.Value.PatternOrDefault;
            if (pattern == null || pattern.IsReadOnly) return false;
            pattern.SetValue(text ?? string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryTextInsert(AutomationElement element, string? text)
    {
        try
        {
            // Try to use the TextBox wrapper which handles TextPattern internally
            var textBox = element.AsTextBox();
            if (textBox != null)
            {
                textBox.Text = text ?? string.Empty;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryToggle(AutomationElement element)
    {
        var pattern = element.Patterns.Toggle.PatternOrDefault;
        if (pattern == null) return false;
        pattern.Toggle();
        return true;
    }

    public static bool TrySelect(AutomationElement element)
    {
        var pattern = element.Patterns.SelectionItem.PatternOrDefault;
        if (pattern == null) return false;
        pattern.Select();
        return true;
    }

    public static bool TryExpand(AutomationElement element, ExpandCollapseState target)
    {
        var pattern = element.Patterns.ExpandCollapse.PatternOrDefault;
        if (pattern == null) return false;
        if (target == ExpandCollapseState.Expanded) pattern.Expand(); else pattern.Collapse();
        return true;
    }

    public static bool TryScrollItem(AutomationElement element)
    {
        var pattern = element.Patterns.ScrollItem.PatternOrDefault;
        if (pattern == null) return false;
        pattern.ScrollIntoView();
        return true;
    }

    public static bool TryScroll(AutomationElement element, double? horizontalPercent, double? verticalPercent)
    {
        try
        {
            var pattern = element.Patterns.Scroll.PatternOrDefault;
            if (pattern == null) return false;

            // If verticalPercent is provided and > 100, treat it as a scroll delta (positive = down, negative = up)
            double v;
            if (verticalPercent.HasValue)
            {
                if (Math.Abs(verticalPercent.Value) > 100)
                {
                    // Treat as delta - scroll by amount
                    var current = pattern.VerticalScrollPercent;
                    var delta = Math.Sign(verticalPercent.Value) * Math.Min(Math.Abs(verticalPercent.Value) / 10.0, 50.0); // Limit delta
                    v = Math.Clamp(current + delta, 0, 100);
                }
                else
                {
                    v = Math.Clamp(verticalPercent.Value, 0, 100);
                }
            }
            else
            {
                v = pattern.VerticalScrollPercent;
            }

            var h = horizontalPercent.HasValue ? Math.Clamp(horizontalPercent.Value, 0, 100) : pattern.HorizontalScrollPercent;
            pattern.SetScrollPercent(h, v);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryRangeValue(AutomationElement element, double value)
    {
        var pattern = element.Patterns.RangeValue.PatternOrDefault;
        if (pattern == null || pattern.IsReadOnly) return false;
        var bounded = Math.Min(pattern.Maximum, Math.Max(pattern.Minimum, value));
        pattern.SetValue(bounded);
        return true;
    }

    public static bool TryWindow(AutomationElement element, Action<IWindowPattern> act)
    {
        var pattern = element.Patterns.Window.PatternOrDefault;
        if (pattern == null) return false;
        act(pattern);
        return true;
    }

    public static bool TryTransform(AutomationElement element, Action<ITransformPattern> act)
    {
        var pattern = element.Patterns.Transform.PatternOrDefault;
        if (pattern == null) return false;
        act(pattern);
        return true;
    }

    public static bool TrySelection(AutomationElement element, Func<ISelectionPattern, bool> act)
    {
        var pattern = element.Patterns.Selection.PatternOrDefault;
        if (pattern == null) return false;
        return act(pattern);
    }

    public static bool TrySelectionItem(AutomationElement element)
    {
        var pattern = element.Patterns.SelectionItem.PatternOrDefault;
        if (pattern == null) return false;
        pattern.Select();
        return true;
    }

    /// <summary>
    /// Selects an item and then clicks it. Use for Click actions on selection items
    /// like LISTITEM where Select() alone only highlights without invoking.
    /// </summary>
    public static bool TrySelectionItemWithClick(AutomationElement element)
    {
        var pattern = element.Patterns.SelectionItem.PatternOrDefault;
        if (pattern == null) return false;
        pattern.Select();
        // After selecting, perform a click to actually invoke the item
        try
        {
            element.Click(true);
            return true;
        }
        catch
        {
            // If click fails, the selection still succeeded - return true
            // as the element was at least selected
            return true;
        }
    }

    public static bool TryLegacyAccessibleAction(AutomationElement element)
    {
        try
        {
            var pattern = element.Patterns.LegacyIAccessible.PatternOrDefault;
            if (pattern == null) return false;
            pattern.DoDefaultAction();
            return true;
        }
        catch
        {
            // DoDefaultAction can throw on elements that don't support it
            return false;
        }
    }

    public static bool TryExpandForActionType(AutomationElement element, ActionType actionType)
    {
        if (actionType == ActionType.Click || actionType == ActionType.Focus)
        {
            var expand = element.Patterns.ExpandCollapse.PatternOrDefault;
            if (expand != null && expand.ExpandCollapseState == ExpandCollapseState.Collapsed)
            {
                expand.Expand();
                return true;
            }
        }
        return false;
    }
}

