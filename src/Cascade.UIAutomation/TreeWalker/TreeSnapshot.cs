using System.Text.Json;
using System.Text.Json.Serialization;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.TreeWalker;

/// <summary>
/// A point-in-time snapshot of a UI element tree.
/// </summary>
public class TreeSnapshot
{
    /// <summary>
    /// Gets the root element snapshot.
    /// </summary>
    public ElementSnapshot Root { get; }

    /// <summary>
    /// Gets the time when the snapshot was captured.
    /// </summary>
    public DateTime CapturedAt { get; }

    /// <summary>
    /// Gets the total number of elements in the snapshot.
    /// </summary>
    public int TotalElements { get; }

    /// <summary>
    /// Gets the maximum depth of the tree.
    /// </summary>
    public int MaxDepth { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TreeSnapshot"/> class.
    /// </summary>
    public TreeSnapshot(ElementSnapshot root, DateTime capturedAt, int totalElements, int maxDepth)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        CapturedAt = capturedAt;
        TotalElements = totalElements;
        MaxDepth = maxDepth;
    }

    /// <summary>
    /// Finds an element by its runtime ID.
    /// </summary>
    public ElementSnapshot? FindByRuntimeId(string runtimeId)
    {
        return FindRecursive(Root, e => e.RuntimeId == runtimeId);
    }

    /// <summary>
    /// Finds an element by its automation ID.
    /// </summary>
    public ElementSnapshot? FindByAutomationId(string automationId)
    {
        return FindRecursive(Root, e => e.AutomationId == automationId);
    }

    /// <summary>
    /// Finds all elements matching a control type.
    /// </summary>
    public IReadOnlyList<ElementSnapshot> FindByControlType(ControlType controlType)
    {
        var results = new List<ElementSnapshot>();
        var controlTypeId = (int)controlType;
        FindAllRecursive(Root, e => e.ControlTypeId == controlTypeId, results);
        return results;
    }

    /// <summary>
    /// Finds all elements matching the specified predicate.
    /// </summary>
    public IReadOnlyList<ElementSnapshot> FindAll(Func<ElementSnapshot, bool> predicate)
    {
        var results = new List<ElementSnapshot>();
        FindAllRecursive(Root, predicate, results);
        return results;
    }

    /// <summary>
    /// Returns all elements in the snapshot as a flat list.
    /// </summary>
    public IReadOnlyList<ElementSnapshot> GetAllElements()
    {
        var results = new List<ElementSnapshot>();
        CollectAllRecursive(Root, results);
        return results;
    }

    /// <summary>
    /// Serializes this snapshot to JSON.
    /// </summary>
    public string ToJson(bool indented = false)
    {
        var data = new TreeSnapshotData
        {
            Root = Root,
            CapturedAt = CapturedAt,
            TotalElements = TotalElements,
            MaxDepth = MaxDepth
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(data, options);
    }

    /// <summary>
    /// Deserializes a snapshot from JSON.
    /// </summary>
    public static TreeSnapshot? FromJson(string json)
    {
        var data = JsonSerializer.Deserialize<TreeSnapshotData>(json);
        if (data?.Root == null)
            return null;

        return new TreeSnapshot(data.Root, data.CapturedAt, data.TotalElements, data.MaxDepth);
    }

    private static ElementSnapshot? FindRecursive(ElementSnapshot element, Func<ElementSnapshot, bool> predicate)
    {
        if (predicate(element))
            return element;

        foreach (var child in element.Children)
        {
            var result = FindRecursive(child, predicate);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void FindAllRecursive(ElementSnapshot element, Func<ElementSnapshot, bool> predicate, List<ElementSnapshot> results)
    {
        if (predicate(element))
            results.Add(element);

        foreach (var child in element.Children)
        {
            FindAllRecursive(child, predicate, results);
        }
    }

    private static void CollectAllRecursive(ElementSnapshot element, List<ElementSnapshot> results)
    {
        results.Add(element);
        foreach (var child in element.Children)
        {
            CollectAllRecursive(child, results);
        }
    }

    /// <summary>
    /// Internal data class for JSON serialization.
    /// </summary>
    private class TreeSnapshotData
    {
        public ElementSnapshot? Root { get; set; }
        public DateTime CapturedAt { get; set; }
        public int TotalElements { get; set; }
        public int MaxDepth { get; set; }
    }
}

