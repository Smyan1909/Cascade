namespace Cascade.CodeGen.Generation;

/// <summary>
/// Defines a workflow consisting of multiple steps.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Name of the workflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the workflow does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of steps to execute.
    /// </summary>
    public IReadOnlyList<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();

    /// <summary>
    /// Input parameters that can be passed to the workflow.
    /// </summary>
    public Dictionary<string, object> InputParameters { get; set; } = new();

    /// <summary>
    /// Output parameters that the workflow produces.
    /// </summary>
    public Dictionary<string, object> OutputParameters { get; set; } = new();

    /// <summary>
    /// How errors should be handled during workflow execution.
    /// </summary>
    public ErrorHandling ErrorHandling { get; set; } = ErrorHandling.StopOnError;
}

