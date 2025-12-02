using Cascade.Database.Enums;

namespace Cascade.Database.Filters;

/// <summary>
/// Filter criteria for querying scripts.
/// </summary>
public class ScriptFilter
{
    /// <summary>
    /// Filter by script type.
    /// </summary>
    public ScriptType? Type { get; set; }

    /// <summary>
    /// Filter by agent ID.
    /// </summary>
    public Guid? AgentId { get; set; }

    /// <summary>
    /// Filter by name (partial match).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Filter to only compiled scripts.
    /// </summary>
    public bool? IsCompiled { get; set; }

    /// <summary>
    /// Filter by created after date.
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Filter by created before date.
    /// </summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Skip this many records (for pagination).
    /// </summary>
    public int Skip { get; set; } = 0;

    /// <summary>
    /// Take this many records (for pagination). 0 = no limit.
    /// </summary>
    public int Take { get; set; } = 0;

    /// <summary>
    /// Order by field name.
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// Whether to order descending.
    /// </summary>
    public bool OrderDescending { get; set; } = false;
}

