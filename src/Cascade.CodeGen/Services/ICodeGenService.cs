using Cascade.CodeGen.Compilation;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Generation;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using ScriptExecutionContext = Cascade.CodeGen.Execution.ExecutionContext;

namespace Cascade.CodeGen.Services;

public interface ICodeGenService
{
    Task<GeneratedCode> GenerateActionAsync(ActionDefinition action, CancellationToken cancellationToken = default);
    Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task<Script> SaveGeneratedScriptAsync(string name, string description, ScriptType type, GeneratedCode generatedCode, CancellationToken cancellationToken = default);
    Task<CompilationResult> CompileAsync(GeneratedCode code, CancellationToken cancellationToken = default);
    Task<ExecutionResult> ExecuteAsync(Guid scriptId, AutomationCallContext callContext, ScriptExecutionContext executionContext, CancellationToken cancellationToken = default);
}

