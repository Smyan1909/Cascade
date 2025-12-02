namespace Cascade.CodeGen.Generation;

/// <summary>
/// Interface for generating code from definitions.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates code for a single action.
    /// </summary>
    Task<GeneratedCode> GenerateActionAsync(ActionDefinition action);

    /// <summary>
    /// Generates code for multiple actions.
    /// </summary>
    Task<GeneratedCode> GenerateActionsAsync(IEnumerable<ActionDefinition> actions);

    /// <summary>
    /// Generates code for a workflow.
    /// </summary>
    Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow);

    /// <summary>
    /// Generates code for an agent class.
    /// </summary>
    Task<GeneratedCode> GenerateAgentAsync(AgentDefinition agent);

    /// <summary>
    /// Optimizes generated code.
    /// </summary>
    Task<string> OptimizeAsync(string sourceCode);
}

