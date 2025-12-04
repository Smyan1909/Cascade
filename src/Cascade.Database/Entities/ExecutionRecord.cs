namespace Cascade.Database.Entities;

/// <summary>
/// Represents a record of an agent execution.
/// </summary>
public class ExecutionRecord
{
    /// <summary>
    /// Unique identifier for the execution record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the agent that was executed.
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Optional user ID who initiated the execution.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Optional session ID for grouping related executions.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Description of the task that was executed.
    /// </summary>
    public string TaskDescription { get; set; } = string.Empty;

    /// <summary>
    /// Whether the execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Summary of what was accomplished.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// When the execution started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the execution completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Duration of execution in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// JSON data containing the result of the execution.
    /// </summary>
    public string? ResultData { get; set; }

    /// <summary>
    /// Log entries from the execution.
    /// Stored as JSON.
    /// </summary>
    public List<string> Logs { get; set; } = new();

    // Navigation properties

    /// <summary>
    /// The agent that was executed.
    /// </summary>
    public Agent Agent { get; set; } = null!;

    /// <summary>
    /// Collection of steps executed during this execution.
    /// </summary>
    public ICollection<ExecutionStep> Steps { get; set; } = new List<ExecutionStep>();

    /// <summary>
    /// Sessions associated with this execution (handles reacquire/retry cases).
    /// </summary>
    public ICollection<AutomationSession> Sessions { get; set; } = new List<AutomationSession>();
}

