using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Templates;

namespace Cascade.CodeGen.Recording;

/// <summary>
/// Represents a session of recorded actions.
/// </summary>
public class RecordingSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public RecordingState State { get; set; } = RecordingState.NotStarted;
    public IReadOnlyList<RecordedAction> Actions { get; set; } = new List<RecordedAction>();
    public RecordingOptions Options { get; set; } = new();

    /// <summary>
    /// Converts the recording session to generated code.
    /// </summary>
    public GeneratedCode ToCode(string className)
    {
        // This would be implemented using a code generator
        // For now, return a basic structure
        throw new NotImplementedException("ToCode requires code generator - implement via RecordingService");
    }

    /// <summary>
    /// Converts the recording session to a workflow definition.
    /// </summary>
    public WorkflowDefinition ToWorkflow(string workflowName)
    {
        var steps = Actions
            .OrderBy(a => a.Index)
            .Select((action, index) => new WorkflowStep
            {
                Order = index + 1,
                Name = $"Step{index + 1}",
                Description = $"Recorded {action.Type} action",
                Action = ConvertToActionDefinition(action),
                DelayAfter = action.DurationSincePrevious
            })
            .ToList();

        return new WorkflowDefinition
        {
            Name = workflowName,
            Description = $"Workflow generated from recording session {SessionId}",
            Steps = steps
        };
    }

    private ActionDefinition ConvertToActionDefinition(RecordedAction action)
    {
        // This is a simplified conversion - would need ElementLocator creation from ElementSnapshot
        return new ActionDefinition
        {
            Name = $"RecordedAction{action.Index}",
            Type = action.Type,
            Parameters = action.Parameters,
            Delay = action.DurationSincePrevious
        };
    }
}

