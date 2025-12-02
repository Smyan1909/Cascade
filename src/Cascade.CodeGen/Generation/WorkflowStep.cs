namespace Cascade.CodeGen.Generation;

/// <summary>
/// A single step in a workflow.
/// </summary>
public class WorkflowStep
{
    /// <summary>
    /// Order of execution (lower numbers execute first).
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Name of the step.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this step does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The action to perform in this step.
    /// </summary>
    public ActionDefinition Action { get; set; } = null!;

    /// <summary>
    /// Optional C# expression condition that must evaluate to true for this step to execute.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Optional delay to wait after this step completes.
    /// </summary>
    public TimeSpan? DelayAfter { get; set; }

    /// <summary>
    /// Optional step to execute if this step succeeds.
    /// </summary>
    public WorkflowStep? OnSuccess { get; set; }

    /// <summary>
    /// Optional step to execute if this step fails.
    /// </summary>
    public WorkflowStep? OnFailure { get; set; }
}

