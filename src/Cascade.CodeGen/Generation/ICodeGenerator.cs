namespace Cascade.CodeGen.Generation;

public interface ICodeGenerator
{
    Task<GeneratedCode> GenerateActionAsync(ActionDefinition action, CancellationToken cancellationToken = default);
    Task<GeneratedCode> GenerateActionsAsync(IEnumerable<ActionDefinition> actions, CancellationToken cancellationToken = default);
    Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task<string> OptimizeAsync(string sourceCode, CancellationToken cancellationToken = default);
}

