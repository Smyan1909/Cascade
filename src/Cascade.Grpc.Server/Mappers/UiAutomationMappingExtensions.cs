using Cascade.Grpc.UIAutomation;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Models;
using Cascade.UIAutomation.Patterns;
using System.Reflection;
using System.Windows.Automation;
using System.Linq;
using DrawingRectangle = System.Drawing.Rectangle;
using ProtoElement = Cascade.Grpc.UIAutomation.Element;
using ProtoTreeNode = Cascade.Grpc.UIAutomation.TreeNode;
using ProtoRectangle = Cascade.Grpc.Rectangle;
using ProtoToggleState = Cascade.Grpc.UIAutomation.ToggleState;
using ProtoTreeResponse = Cascade.Grpc.UIAutomation.TreeSnapshotResponse;
using ProtoTimestamp = Cascade.Grpc.Timestamp;
using DomainSearchCriteria = Cascade.UIAutomation.Discovery.SearchCriteria;
using ProtoSearchCriteria = Cascade.Grpc.UIAutomation.SearchCriteria;

namespace Cascade.Grpc.Server.Mappers;

internal static class UiAutomationMappingExtensions
{
    private static readonly IReadOnlyDictionary<string, ControlType> ControlTypeLookup = typeof(ControlType)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => p.PropertyType == typeof(ControlType))
        .ToDictionary(p => p.Name, p => (ControlType)p.GetValue(null)!, StringComparer.OrdinalIgnoreCase);

    public static DomainSearchCriteria ToDomainCriteria(this ProtoSearchCriteria? criteria)
    {
        var domain = new DomainSearchCriteria();
        if (criteria is null)
        {
            return domain;
        }

        if (!string.IsNullOrWhiteSpace(criteria.AutomationId))
        {
            domain.AutomationId = criteria.AutomationId;
        }

        if (!string.IsNullOrWhiteSpace(criteria.Name))
        {
            domain.Name = criteria.Name;
        }

        if (!string.IsNullOrWhiteSpace(criteria.NameContains))
        {
            domain.NameContains = criteria.NameContains;
        }

        if (!string.IsNullOrWhiteSpace(criteria.ClassName))
        {
            domain.ClassName = criteria.ClassName;
        }

        if (!string.IsNullOrWhiteSpace(criteria.ControlType) && ControlTypeLookup.TryGetValue(criteria.ControlType, out var controlType))
        {
            domain.ControlType = controlType;
        }

        if (criteria.EnabledOnly)
        {
            domain.IsEnabled = true;
        }

        if (criteria.VisibleOnly)
        {
            domain.IsOffscreen = false;
        }

        return domain;
    }

    public static ProtoElement ToProtoElement(this IUIElement element)
    {
        return new ProtoElement
        {
            RuntimeId = element.RuntimeId,
            AutomationId = element.AutomationId ?? string.Empty,
            Name = element.Name ?? string.Empty,
            ClassName = element.ClassName ?? string.Empty,
            ControlType = element.ControlType?.ProgrammaticName ?? string.Empty,
            BoundingRectangle = element.BoundingRectangle.ToProtoRectangle(),
            IsEnabled = element.IsEnabled,
            IsOffscreen = element.IsOffscreen,
            HasKeyboardFocus = element.HasKeyboardFocus,
            ProcessId = element.ProcessId,
        }.WithPatterns(element.SupportedPatterns);
    }

    private static ProtoElement WithPatterns(this ProtoElement element, IReadOnlyList<PatternType> patterns)
    {
        element.SupportedPatterns.Clear();
        foreach (var pattern in patterns)
        {
            element.SupportedPatterns.Add(pattern.ToString());
        }

        return element;
    }

    public static ProtoRectangle ToProtoRectangle(this DrawingRectangle rectangle)
    {
        return new ProtoRectangle
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height
        };
    }

    public static ProtoToggleState ToProto(this System.Windows.Automation.ToggleState state)
    {
        return state switch
        {
            System.Windows.Automation.ToggleState.Off => ProtoToggleState.Off,
            System.Windows.Automation.ToggleState.On => ProtoToggleState.On,
            System.Windows.Automation.ToggleState.Indeterminate => ProtoToggleState.Indeterminate,
            _ => ProtoToggleState.Off
        };
    }

    public static ProtoTreeResponse ToProtoResponse(this TreeSnapshot snapshot)
    {
        return new ProtoTreeResponse
        {
            Result = ProtoResults.Success(),
            Root = snapshot.Root.ToProtoTreeNode(),
            TotalElements = snapshot.TotalElements,
            CapturedAt = snapshot.CapturedAt.ToTimestamp()
        };
    }

    private static ProtoTreeNode ToProtoTreeNode(this ElementSnapshot snapshot)
    {
        var node = new ProtoTreeNode
        {
            Element = new ProtoElement
            {
                RuntimeId = snapshot.RuntimeId,
                AutomationId = snapshot.AutomationId ?? string.Empty,
                Name = snapshot.Name ?? string.Empty,
                ClassName = snapshot.ClassName ?? string.Empty,
                ControlType = snapshot.ControlType,
                BoundingRectangle = snapshot.BoundingRectangle.ToProtoRectangle(),
                IsEnabled = snapshot.IsEnabled,
                IsOffscreen = snapshot.IsOffscreen
            }
        };

        node.Element.SupportedPatterns.Add(snapshot.SupportedPatterns);
        foreach (var child in snapshot.Children)
        {
            node.Children.Add(child.ToProtoTreeNode());
        }

        return node;
    }

    public static ProtoTimestamp ToTimestamp(this DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
        var offset = new DateTimeOffset(utc);
        var seconds = offset.ToUnixTimeSeconds();
        var nanos = (int)((offset - DateTimeOffset.FromUnixTimeSeconds(seconds)).Ticks * 100);
        return new ProtoTimestamp
        {
            Seconds = seconds,
            Nanos = nanos
        };
    }
}

