using Cascade.Database.Enums;

namespace Cascade.Database.Entities;

/// <summary>
/// Represents an AI agent that can automate tasks in a target application.
/// </summary>
public class Agent
{
    /// <summary>
    /// Unique identifier for the agent.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the agent.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the agent does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Name or identifier of the target application.
    /// </summary>
    public string TargetApplication { get; set; } = string.Empty;

    /// <summary>
    /// Currently active version string.
    /// </summary>
    public string ActiveVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Current status of the agent.
    /// </summary>
    public AgentStatus Status { get; set; } = AgentStatus.Active;

    /// <summary>
    /// List of capabilities/skills the agent has.
    /// Stored as JSON.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Instructions for using the agent.
    /// </summary>
    public string InstructionList { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata key-value pairs.
    /// Stored as JSON.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// When the agent was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the agent was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// When the agent was last executed.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Collection of version snapshots for this agent.
    /// </summary>
    public ICollection<AgentVersion> Versions { get; set; } = new List<AgentVersion>();

    /// <summary>
    /// Collection of scripts associated with this agent.
    /// </summary>
    public ICollection<Script> Scripts { get; set; } = new List<Script>();

    /// <summary>
    /// Collection of execution records for this agent.
    /// </summary>
    public ICollection<ExecutionRecord> Executions { get; set; } = new List<ExecutionRecord>();
}

