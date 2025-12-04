using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade.UIAutomation.Models;

public sealed class ElementSnapshot
{
    public string RuntimeId { get; init; } = string.Empty;
    public string? AutomationId { get; init; }
    public string? Name { get; init; }
    public string? ClassName { get; init; }
    public string ControlType { get; init; } = string.Empty;
    public Rectangle BoundingRectangle { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsOffscreen { get; init; }
    public List<string> SupportedPatterns { get; init; } = [];
    public List<ElementSnapshot> Children { get; init; } = [];
}

public sealed class TreeSnapshot
{
    public ElementSnapshot Root { get; init; } = new();
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    public int TotalElements { get; init; }

    public ElementSnapshot? FindByRuntimeId(string runtimeId)
    {
        return Traverse().FirstOrDefault(e => string.Equals(e.RuntimeId, runtimeId, StringComparison.Ordinal));
    }

    public ElementSnapshot? FindByAutomationId(string automationId)
    {
        return Traverse().FirstOrDefault(e => string.Equals(e.AutomationId, automationId, StringComparison.Ordinal));
    }

    public IReadOnlyList<ElementSnapshot> FindByControlType(string controlType)
    {
        return Traverse()
            .Where(e => string.Equals(e.ControlType, controlType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    public static TreeSnapshot FromJson(string json)
    {
        return JsonSerializer.Deserialize<TreeSnapshot>(json) ?? throw new InvalidOperationException("Unable to deserialize tree snapshot.");
    }

    private IEnumerable<ElementSnapshot> Traverse()
    {
        var stack = new Stack<ElementSnapshot>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            foreach (var child in current.Children)
            {
                stack.Push(child);
            }
        }
    }
}


