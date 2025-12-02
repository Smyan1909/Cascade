using Cascade.Database.Enums;

namespace Cascade.Database.Entities;

/// <summary>
/// Represents an exploration session for discovering an application's UI.
/// </summary>
public class ExplorationSession
{
    /// <summary>
    /// Unique identifier for the session.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name or identifier of the target application being explored.
    /// </summary>
    public string TargetApplication { get; set; } = string.Empty;

    /// <summary>
    /// Optional instruction manual or guidance for exploration.
    /// </summary>
    public string? InstructionManual { get; set; }

    /// <summary>
    /// Current status of the exploration.
    /// </summary>
    public ExplorationStatus Status { get; set; }

    /// <summary>
    /// Progress percentage (0.0 to 1.0).
    /// </summary>
    public float Progress { get; set; }

    /// <summary>
    /// Goals to be achieved during exploration.
    /// Stored as JSON.
    /// </summary>
    public List<ExplorationGoal> Goals { get; set; } = new();

    /// <summary>
    /// IDs of goals that have been completed.
    /// Stored as JSON.
    /// </summary>
    public List<string> CompletedGoals { get; set; } = new();

    /// <summary>
    /// IDs of goals that have failed.
    /// Stored as JSON.
    /// </summary>
    public List<string> FailedGoals { get; set; } = new();

    /// <summary>
    /// When the exploration started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the exploration completed (if finished).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// ID of the agent generated from this exploration (if any).
    /// </summary>
    public Guid? GeneratedAgentId { get; set; }

    // Navigation properties

    /// <summary>
    /// Collection of results captured during exploration.
    /// </summary>
    public ICollection<ExplorationResult> Results { get; set; } = new List<ExplorationResult>();

    /// <summary>
    /// The agent generated from this exploration (if any).
    /// </summary>
    public Agent? GeneratedAgent { get; set; }
}

