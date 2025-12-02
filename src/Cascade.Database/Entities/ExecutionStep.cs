namespace Cascade.Database.Entities;

/// <summary>
/// Represents a single step within an agent execution.
/// </summary>
public class ExecutionStep
{
    /// <summary>
    /// Unique identifier for the step.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the parent execution record.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Order of this step in the execution sequence.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Name or description of the action performed.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// JSON data containing parameters for the action.
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Whether this step was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if step failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// JSON data containing the result of the step.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Duration of this step in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Screenshot captured after this step (if any).
    /// </summary>
    public byte[]? Screenshot { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent execution record.
    /// </summary>
    public ExecutionRecord Execution { get; set; } = null!;
}

