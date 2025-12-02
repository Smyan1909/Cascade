using System.Drawing;
using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Discovery;

/// <summary>
/// Defines criteria for searching UI elements.
/// </summary>
public class SearchCriteria
{
    /// <summary>
    /// Gets or sets the AutomationId to match.
    /// </summary>
    public string? AutomationId { get; set; }

    /// <summary>
    /// Gets or sets the exact Name to match.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a substring that the Name must contain.
    /// </summary>
    public string? NameContains { get; set; }

    /// <summary>
    /// Gets or sets the ClassName to match.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Gets or sets the ControlType to match.
    /// </summary>
    public ControlType? ControlType { get; set; }

    /// <summary>
    /// Gets or sets whether to match only enabled elements.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether to match only offscreen elements.
    /// </summary>
    public bool? IsOffscreen { get; set; }

    /// <summary>
    /// Gets or sets the bounding rectangle to match.
    /// </summary>
    public Rectangle? BoundingRectangle { get; set; }

    /// <summary>
    /// Gets or sets the process ID to match.
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Gets or sets whether the element should be a content element.
    /// </summary>
    public bool? IsContentElement { get; set; }

    /// <summary>
    /// Gets or sets whether the element should be a control element.
    /// </summary>
    public bool? IsControlElement { get; set; }

    // Internal properties for composite criteria
    internal SearchCriteria? AndCriteria { get; private set; }
    internal SearchCriteria? OrCriteria { get; private set; }
    internal bool IsNegated { get; private set; }

    /// <summary>
    /// Creates a new search criteria that matches both this criteria and another.
    /// </summary>
    public SearchCriteria And(SearchCriteria other)
    {
        var result = Clone();
        result.AndCriteria = other;
        return result;
    }

    /// <summary>
    /// Creates a new search criteria that matches either this criteria or another.
    /// </summary>
    public SearchCriteria Or(SearchCriteria other)
    {
        var result = Clone();
        result.OrCriteria = other;
        return result;
    }

    /// <summary>
    /// Creates a new search criteria that negates this criteria.
    /// </summary>
    public SearchCriteria Not()
    {
        var result = Clone();
        result.IsNegated = !result.IsNegated;
        return result;
    }

    /// <summary>
    /// Creates a search criteria for matching by AutomationId.
    /// </summary>
    public static SearchCriteria ByAutomationId(string id)
        => new() { AutomationId = id };

    /// <summary>
    /// Creates a search criteria for matching by exact Name.
    /// </summary>
    public static SearchCriteria ByName(string name)
        => new() { Name = name };

    /// <summary>
    /// Creates a search criteria for matching by Name containing a substring.
    /// </summary>
    public static SearchCriteria ByNameContains(string substring)
        => new() { NameContains = substring };

    /// <summary>
    /// Creates a search criteria for matching by ClassName.
    /// </summary>
    public static SearchCriteria ByClassName(string className)
        => new() { ClassName = className };

    /// <summary>
    /// Creates a search criteria for matching by ControlType.
    /// </summary>
    public static SearchCriteria ByControlType(ControlType controlType)
        => new() { ControlType = controlType };

    /// <summary>
    /// Creates a search criteria for matching by ProcessId.
    /// </summary>
    public static SearchCriteria ByProcessId(int processId)
        => new() { ProcessId = processId };

    /// <summary>
    /// Creates an empty search criteria that matches all elements.
    /// </summary>
    public static SearchCriteria All => new();

    /// <summary>
    /// Creates a clone of this search criteria.
    /// </summary>
    private SearchCriteria Clone()
    {
        return new SearchCriteria
        {
            AutomationId = AutomationId,
            Name = Name,
            NameContains = NameContains,
            ClassName = ClassName,
            ControlType = ControlType,
            IsEnabled = IsEnabled,
            IsOffscreen = IsOffscreen,
            BoundingRectangle = BoundingRectangle,
            ProcessId = ProcessId,
            IsContentElement = IsContentElement,
            IsControlElement = IsControlElement,
            AndCriteria = AndCriteria,
            OrCriteria = OrCriteria,
            IsNegated = IsNegated
        };
    }

    /// <summary>
    /// Returns a string representation of this search criteria.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(AutomationId))
            parts.Add($"AutomationId='{AutomationId}'");
        if (!string.IsNullOrEmpty(Name))
            parts.Add($"Name='{Name}'");
        if (!string.IsNullOrEmpty(NameContains))
            parts.Add($"NameContains='{NameContains}'");
        if (!string.IsNullOrEmpty(ClassName))
            parts.Add($"ClassName='{ClassName}'");
        if (ControlType.HasValue)
            parts.Add($"ControlType={ControlType}");
        if (IsEnabled.HasValue)
            parts.Add($"IsEnabled={IsEnabled}");
        if (IsOffscreen.HasValue)
            parts.Add($"IsOffscreen={IsOffscreen}");
        if (ProcessId.HasValue)
            parts.Add($"ProcessId={ProcessId}");

        var result = parts.Count > 0 ? string.Join(", ", parts) : "All";

        if (IsNegated)
            result = $"NOT({result})";
        if (AndCriteria != null)
            result = $"({result}) AND ({AndCriteria})";
        if (OrCriteria != null)
            result = $"({result}) OR ({OrCriteria})";

        return result;
    }
}

