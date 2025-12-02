using Cascade.Database.Enums;

namespace Cascade.Database.Filters;

/// <summary>
/// Filter criteria for querying exploration sessions.
/// </summary>
public class ExplorationFilter
{
    /// <summary>
    /// Filter by target application (partial match).
    /// </summary>
    public string? TargetApplication { get; set; }

    /// <summary>
    /// Filter by exploration status.
    /// </summary>
    public ExplorationStatus? Status { get; set; }

    /// <summary>
    /// Filter by started after date.
    /// </summary>
    public DateTime? StartedAfter { get; set; }

    /// <summary>
    /// Filter by started before date.
    /// </summary>
    public DateTime? StartedBefore { get; set; }

    /// <summary>
    /// Filter to only sessions that generated an agent.
    /// </summary>
    public bool? HasGeneratedAgent { get; set; }

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

