namespace Cascade.Database.Entities;

/// <summary>
/// Represents a version snapshot of an agent's state.
/// </summary>
public class AgentVersion
{
    /// <summary>
    /// Unique identifier for the version.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the parent agent.
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Version string (e.g., "1.0.0", "1.1.0").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes describing this version.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this is the currently active version.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Snapshot of the instruction list at this version.
    /// </summary>
    public string InstructionListSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of capabilities at this version.
    /// Stored as JSON.
    /// </summary>
    public List<string> CapabilitiesSnapshot { get; set; } = new();

    /// <summary>
    /// Snapshot of associated script IDs at this version.
    /// Stored as JSON.
    /// </summary>
    public List<Guid> ScriptIdsSnapshot { get; set; } = new();

    /// <summary>
    /// When this version was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent agent.
    /// </summary>
    public Agent Agent { get; set; } = null!;
}

