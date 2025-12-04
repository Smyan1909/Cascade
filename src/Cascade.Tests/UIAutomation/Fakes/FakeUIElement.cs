using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cascade.Core;
using Cascade.Core.Session;
using Cascade.UIAutomation.Actions;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Models;
using Cascade.UIAutomation.Patterns;
using System.Drawing;
using System.Windows.Automation;

namespace Cascade.Tests.UIAutomation.Fakes;

internal sealed class FakeUIElement : IUIElement
{
    private readonly List<IUIElement> _children = new();
    private readonly List<PatternType> _patterns = new();
    private readonly Rectangle _bounds;
    private readonly SessionHandle _session;
    private readonly VirtualInputChannel _inputChannel;
    private readonly string _runtimeId = Guid.NewGuid().ToString();

    public FakeUIElement(
        string controlType,
        string automationId = "",
        string name = "",
        string className = "",
        int processId = 4242,
        Rectangle? bounds = null,
        IEnumerable<FakeUIElement>? children = null)
    {
        ControlType = ResolveControlType(controlType);
        AutomationId = automationId;
        Name = name;
        ClassName = className;
        ProcessId = processId;
        _bounds = bounds ?? new Rectangle(0, 0, 100, 50);

        _session = new SessionHandle
        {
            SessionId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            VirtualDesktopId = new IntPtr(1),
            UserProfilePath = "test-user",
            DesktopProfile = VirtualDesktopProfile.Default
        };

        _inputChannel = new VirtualInputChannel
        {
            ChannelId = Guid.NewGuid(),
            DevicePath = @"\\.\virtual-input",
            Transport = "hid"
        };

        if (children is not null)
        {
            foreach (var child in children)
            {
                AddChild(child);
            }
        }
    }

    public void AddChild(FakeUIElement child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public SessionHandle Session => _session;
    public VirtualInputChannel InputChannel => _inputChannel;
    public string AutomationId { get; }
    public string Name { get; }
    public string ClassName { get; }
    public ControlType ControlType { get; }
    public string RuntimeId => _runtimeId;
    public int ProcessId { get; }
    public IUIElement? Parent { get; private set; }
    public IReadOnlyList<IUIElement> Children => _children;
    public Rectangle BoundingRectangle => _bounds;
    public Point ClickablePoint => new(_bounds.Left + _bounds.Width / 2, _bounds.Top + _bounds.Height / 2);
    public bool IsOffscreen => false;
    public bool IsEnabled => true;
    public bool HasKeyboardFocus => false;
    public bool IsContentElement => true;
    public bool IsControlElement => true;
    public IReadOnlyList<PatternType> SupportedPatterns => _patterns;

    public IUIElement? FindFirst(SearchCriteria criteria)
    {
        return Enumerate(this).FirstOrDefault(element => Matches(element, criteria));
    }

    public IReadOnlyList<IUIElement> FindAll(SearchCriteria criteria)
    {
        return Enumerate(this).Where(element => Matches(element, criteria)).ToList();
    }

    public Task ClickAsync(ClickType clickType = ClickType.Left) => Task.CompletedTask;
    public Task DoubleClickAsync() => Task.CompletedTask;
    public Task RightClickAsync() => Task.CompletedTask;
    public Task TypeTextAsync(string text) => Task.CompletedTask;
    public Task SetValueAsync(string value) => Task.CompletedTask;
    public Task InvokeAsync() => Task.CompletedTask;
    public Task SetFocusAsync() => Task.CompletedTask;

    public bool TryGetPattern<T>(out T pattern) where T : class
    {
        pattern = null!;
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

    private static ControlType ResolveControlType(string controlType)
    {
        return controlType.ToLowerInvariant() switch
        {
            "window" => ControlType.Window,
            "pane" => ControlType.Pane,
            "button" => ControlType.Button,
            "edit" => ControlType.Edit,
            "list" => ControlType.List,
            "listitem" => ControlType.ListItem,
            _ => ControlType.Custom
        };
    }

    private static IEnumerable<IUIElement> Enumerate(IUIElement root)
    {
        var queue = new Queue<IUIElement>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    private static bool Matches(IUIElement element, SearchCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.AutomationId) &&
            !string.Equals(element.AutomationId, criteria.AutomationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.Name) &&
            !string.Equals(element.Name, criteria.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.NameContains) &&
            (element.Name?.Contains(criteria.NameContains, StringComparison.OrdinalIgnoreCase) != true))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.ClassName) &&
            !string.Equals(element.ClassName, criteria.ClassName, StringComparison.Ordinal))
        {
            return false;
        }

        if (criteria.ControlType is not null &&
            element.ControlType != criteria.ControlType)
        {
            return false;
        }

        if (criteria.IsEnabled is not null && element.IsEnabled != criteria.IsEnabled)
        {
            return false;
        }

        return true;
    }
}

