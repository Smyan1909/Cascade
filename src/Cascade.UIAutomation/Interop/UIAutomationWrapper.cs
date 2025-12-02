using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Enums;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using ControlType = Cascade.UIAutomation.Enums.ControlType;
using TreeScope = Cascade.UIAutomation.Interop.TreeScope;

namespace Cascade.UIAutomation.Interop;

/// <summary>
/// Implementation of UI Automation wrapper using FlaUI library.
/// </summary>
public class UIAutomationWrapper : IUIAutomationWrapper
{
    private readonly UIA3Automation _automation;
    private readonly ITreeWalker _controlViewWalker;
    private readonly ITreeWalker _contentViewWalker;
    private readonly ITreeWalker _rawViewWalker;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAutomationWrapper"/> class.
    /// </summary>
    public UIAutomationWrapper()
    {
        _automation = new UIA3Automation();
        _controlViewWalker = _automation.TreeWalkerFactory.GetControlViewWalker();
        _contentViewWalker = _automation.TreeWalkerFactory.GetContentViewWalker();
        _rawViewWalker = _automation.TreeWalkerFactory.GetRawViewWalker();
    }

    /// <summary>
    /// Gets the underlying FlaUI automation instance.
    /// </summary>
    internal UIA3Automation Automation => _automation;

    /// <inheritdoc />
    public object GetRootElement()
    {
        return _automation.GetDesktop();
    }

    /// <inheritdoc />
    public object? GetFocusedElement()
    {
        try
        {
            return _automation.FocusedElement();
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? ElementFromPoint(int x, int y)
    {
        try
        {
            var point = new System.Drawing.Point(x, y);
            return _automation.FromPoint(point);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? ElementFromHandle(IntPtr hwnd)
    {
        try
        {
            return _automation.FromHandle(hwnd);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object CreateCondition(SearchCriteria criteria)
    {
        var cf = _automation.ConditionFactory;
        var conditions = new List<ConditionBase>();

        if (!string.IsNullOrEmpty(criteria.AutomationId))
        {
            conditions.Add(cf.ByAutomationId(criteria.AutomationId));
        }

        if (!string.IsNullOrEmpty(criteria.Name))
        {
            conditions.Add(cf.ByName(criteria.Name));
        }

        if (!string.IsNullOrEmpty(criteria.ClassName))
        {
            conditions.Add(cf.ByClassName(criteria.ClassName));
        }

        if (criteria.ControlType.HasValue)
        {
            conditions.Add(cf.ByControlType(MapControlType(criteria.ControlType.Value)));
        }

        if (criteria.ProcessId.HasValue)
        {
            conditions.Add(cf.ByProcessId(criteria.ProcessId.Value));
        }

        // Combine all conditions with AND
        ConditionBase result;
        if (conditions.Count == 0)
        {
            result = TrueCondition.Default;
        }
        else if (conditions.Count == 1)
        {
            result = conditions[0];
        }
        else
        {
            result = conditions[0];
            for (int i = 1; i < conditions.Count; i++)
            {
                result = new AndCondition(result, conditions[i]);
            }
        }

        // Handle composite criteria
        if (criteria.AndCriteria != null)
        {
            var andCondition = (ConditionBase)CreateCondition(criteria.AndCriteria);
            result = new AndCondition(result, andCondition);
        }

        if (criteria.OrCriteria != null)
        {
            var orCondition = (ConditionBase)CreateCondition(criteria.OrCriteria);
            result = new OrCondition(result, orCondition);
        }

        if (criteria.IsNegated)
        {
            result = new NotCondition(result);
        }

        return result;
    }

    /// <inheritdoc />
    public object CreateTrueCondition()
    {
        return TrueCondition.Default;
    }

    /// <inheritdoc />
    public object CreateControlViewWalker()
    {
        return _controlViewWalker;
    }

    /// <inheritdoc />
    public object CreateContentViewWalker()
    {
        return _contentViewWalker;
    }

    /// <inheritdoc />
    public object CreateRawViewWalker()
    {
        return _rawViewWalker;
    }

    /// <inheritdoc />
    public object CreateFilteredTreeWalker(object condition)
    {
        return _automation.TreeWalkerFactory.GetCustomTreeWalker((ConditionBase)condition);
    }

    /// <inheritdoc />
    public object? GetPropertyValue(object element, int propertyId)
    {
        var ae = (AutomationElement)element;
        try
        {
            // Use FlaUI property retrieval
            return propertyId switch
            {
                30005 => ae.Name,
                30011 => ae.AutomationId,
                30012 => ae.ClassName,
                30003 => (int)ae.ControlType,
                30010 => ae.IsEnabled,
                30022 => ae.IsOffscreen,
                30008 => ae.Properties.HasKeyboardFocus.ValueOrDefault,
                30002 => ae.Properties.ProcessId.ValueOrDefault,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? GetParent(object element, object treeWalker)
    {
        var ae = (AutomationElement)element;
        var walker = (ITreeWalker)treeWalker;
        try
        {
            return walker.GetParent(ae);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? GetFirstChild(object element, object treeWalker)
    {
        var ae = (AutomationElement)element;
        var walker = (ITreeWalker)treeWalker;
        try
        {
            return walker.GetFirstChild(ae);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? GetLastChild(object element, object treeWalker)
    {
        var ae = (AutomationElement)element;
        var walker = (ITreeWalker)treeWalker;
        try
        {
            return walker.GetLastChild(ae);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? GetNextSibling(object element, object treeWalker)
    {
        var ae = (AutomationElement)element;
        var walker = (ITreeWalker)treeWalker;
        try
        {
            return walker.GetNextSibling(ae);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? GetPreviousSibling(object element, object treeWalker)
    {
        var ae = (AutomationElement)element;
        var walker = (ITreeWalker)treeWalker;
        try
        {
            return walker.GetPreviousSibling(ae);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? FindFirst(object element, TreeScope scope, object condition)
    {
        var ae = (AutomationElement)element;
        var cond = (ConditionBase)condition;
        try
        {
            return ae.FindFirst(MapTreeScope(scope), cond);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object[] FindAll(object element, TreeScope scope, object condition)
    {
        var ae = (AutomationElement)element;
        var cond = (ConditionBase)condition;
        try
        {
            var results = ae.FindAll(MapTreeScope(scope), cond);
            return results?.Cast<object>().ToArray() ?? Array.Empty<object>();
        }
        catch
        {
            return Array.Empty<object>();
        }
    }

    /// <inheritdoc />
    public object? GetPattern(object element, int patternId)
    {
        var ae = (AutomationElement)element;
        try
        {
            return patternId switch
            {
                10000 => ae.Patterns.Invoke.PatternOrDefault,
                10002 => ae.Patterns.Value.PatternOrDefault,
                10015 => ae.Patterns.Toggle.PatternOrDefault,
                10001 => ae.Patterns.Selection.PatternOrDefault,
                10010 => ae.Patterns.SelectionItem.PatternOrDefault,
                10005 => ae.Patterns.ExpandCollapse.PatternOrDefault,
                10004 => ae.Patterns.Scroll.PatternOrDefault,
                10017 => ae.Patterns.ScrollItem.PatternOrDefault,
                10003 => ae.Patterns.RangeValue.PatternOrDefault,
                10009 => ae.Patterns.Window.PatternOrDefault,
                10016 => ae.Patterns.Transform.PatternOrDefault,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SetFocus(object element)
    {
        var ae = (AutomationElement)element;
        ae.Focus();
    }

    /// <inheritdoc />
    public int[]? GetRuntimeId(object element)
    {
        var ae = (AutomationElement)element;
        try
        {
            return ae.Properties.RuntimeId.ValueOrDefault;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _automation.Dispose();
            _disposed = true;
        }
    }

    private static FlaUI.Core.Definitions.ControlType MapControlType(ControlType controlType)
    {
        return controlType switch
        {
            ControlType.Button => FlaUI.Core.Definitions.ControlType.Button,
            ControlType.Calendar => FlaUI.Core.Definitions.ControlType.Calendar,
            ControlType.CheckBox => FlaUI.Core.Definitions.ControlType.CheckBox,
            ControlType.ComboBox => FlaUI.Core.Definitions.ControlType.ComboBox,
            ControlType.Edit => FlaUI.Core.Definitions.ControlType.Edit,
            ControlType.Hyperlink => FlaUI.Core.Definitions.ControlType.Hyperlink,
            ControlType.Image => FlaUI.Core.Definitions.ControlType.Image,
            ControlType.ListItem => FlaUI.Core.Definitions.ControlType.ListItem,
            ControlType.List => FlaUI.Core.Definitions.ControlType.List,
            ControlType.Menu => FlaUI.Core.Definitions.ControlType.Menu,
            ControlType.MenuBar => FlaUI.Core.Definitions.ControlType.MenuBar,
            ControlType.MenuItem => FlaUI.Core.Definitions.ControlType.MenuItem,
            ControlType.ProgressBar => FlaUI.Core.Definitions.ControlType.ProgressBar,
            ControlType.RadioButton => FlaUI.Core.Definitions.ControlType.RadioButton,
            ControlType.ScrollBar => FlaUI.Core.Definitions.ControlType.ScrollBar,
            ControlType.Slider => FlaUI.Core.Definitions.ControlType.Slider,
            ControlType.Spinner => FlaUI.Core.Definitions.ControlType.Spinner,
            ControlType.StatusBar => FlaUI.Core.Definitions.ControlType.StatusBar,
            ControlType.Tab => FlaUI.Core.Definitions.ControlType.Tab,
            ControlType.TabItem => FlaUI.Core.Definitions.ControlType.TabItem,
            ControlType.Text => FlaUI.Core.Definitions.ControlType.Text,
            ControlType.ToolBar => FlaUI.Core.Definitions.ControlType.ToolBar,
            ControlType.ToolTip => FlaUI.Core.Definitions.ControlType.ToolTip,
            ControlType.Tree => FlaUI.Core.Definitions.ControlType.Tree,
            ControlType.TreeItem => FlaUI.Core.Definitions.ControlType.TreeItem,
            ControlType.Custom => FlaUI.Core.Definitions.ControlType.Custom,
            ControlType.Group => FlaUI.Core.Definitions.ControlType.Group,
            ControlType.Thumb => FlaUI.Core.Definitions.ControlType.Thumb,
            ControlType.DataGrid => FlaUI.Core.Definitions.ControlType.DataGrid,
            ControlType.DataItem => FlaUI.Core.Definitions.ControlType.DataItem,
            ControlType.Document => FlaUI.Core.Definitions.ControlType.Document,
            ControlType.SplitButton => FlaUI.Core.Definitions.ControlType.SplitButton,
            ControlType.Window => FlaUI.Core.Definitions.ControlType.Window,
            ControlType.Pane => FlaUI.Core.Definitions.ControlType.Pane,
            ControlType.Header => FlaUI.Core.Definitions.ControlType.Header,
            ControlType.HeaderItem => FlaUI.Core.Definitions.ControlType.HeaderItem,
            ControlType.Table => FlaUI.Core.Definitions.ControlType.Table,
            ControlType.TitleBar => FlaUI.Core.Definitions.ControlType.TitleBar,
            ControlType.Separator => FlaUI.Core.Definitions.ControlType.Separator,
            _ => FlaUI.Core.Definitions.ControlType.Custom
        };
    }

    private static FlaUI.Core.Definitions.TreeScope MapTreeScope(TreeScope scope)
    {
        return scope switch
        {
            TreeScope.Element => FlaUI.Core.Definitions.TreeScope.Element,
            TreeScope.Children => FlaUI.Core.Definitions.TreeScope.Children,
            TreeScope.Descendants => FlaUI.Core.Definitions.TreeScope.Descendants,
            TreeScope.Parent => FlaUI.Core.Definitions.TreeScope.Parent,
            TreeScope.Ancestors => FlaUI.Core.Definitions.TreeScope.Ancestors,
            TreeScope.Subtree => FlaUI.Core.Definitions.TreeScope.Subtree,
            _ => FlaUI.Core.Definitions.TreeScope.Subtree
        };
    }
}

/// <summary>
/// UI Automation property IDs.
/// </summary>
internal static class UIA_PropertyIds
{
    public const int UIA_RuntimeIdPropertyId = 30000;
    public const int UIA_BoundingRectanglePropertyId = 30001;
    public const int UIA_ProcessIdPropertyId = 30002;
    public const int UIA_ControlTypePropertyId = 30003;
    public const int UIA_LocalizedControlTypePropertyId = 30004;
    public const int UIA_NamePropertyId = 30005;
    public const int UIA_AcceleratorKeyPropertyId = 30006;
    public const int UIA_AccessKeyPropertyId = 30007;
    public const int UIA_HasKeyboardFocusPropertyId = 30008;
    public const int UIA_IsKeyboardFocusablePropertyId = 30009;
    public const int UIA_IsEnabledPropertyId = 30010;
    public const int UIA_AutomationIdPropertyId = 30011;
    public const int UIA_ClassNamePropertyId = 30012;
    public const int UIA_HelpTextPropertyId = 30013;
    public const int UIA_ClickablePointPropertyId = 30014;
    public const int UIA_CulturePropertyId = 30015;
    public const int UIA_IsControlElementPropertyId = 30016;
    public const int UIA_IsContentElementPropertyId = 30017;
    public const int UIA_LabeledByPropertyId = 30018;
    public const int UIA_IsPasswordPropertyId = 30019;
    public const int UIA_NativeWindowHandlePropertyId = 30020;
    public const int UIA_ItemTypePropertyId = 30021;
    public const int UIA_IsOffscreenPropertyId = 30022;
    public const int UIA_OrientationPropertyId = 30023;
    public const int UIA_FrameworkIdPropertyId = 30024;
    public const int UIA_IsRequiredForFormPropertyId = 30025;
    public const int UIA_ItemStatusPropertyId = 30026;
}

/// <summary>
/// UI Automation pattern IDs.
/// </summary>
internal static class UIA_PatternIds
{
    public const int UIA_InvokePatternId = 10000;
    public const int UIA_SelectionPatternId = 10001;
    public const int UIA_ValuePatternId = 10002;
    public const int UIA_RangeValuePatternId = 10003;
    public const int UIA_ScrollPatternId = 10004;
    public const int UIA_ExpandCollapsePatternId = 10005;
    public const int UIA_GridPatternId = 10006;
    public const int UIA_GridItemPatternId = 10007;
    public const int UIA_MultipleViewPatternId = 10008;
    public const int UIA_WindowPatternId = 10009;
    public const int UIA_SelectionItemPatternId = 10010;
    public const int UIA_DockPatternId = 10011;
    public const int UIA_TablePatternId = 10012;
    public const int UIA_TableItemPatternId = 10013;
    public const int UIA_TextPatternId = 10014;
    public const int UIA_TogglePatternId = 10015;
    public const int UIA_TransformPatternId = 10016;
    public const int UIA_ScrollItemPatternId = 10017;
    public const int UIA_LegacyIAccessiblePatternId = 10018;
}
