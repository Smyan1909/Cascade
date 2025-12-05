namespace Cascade.CodeGen.Execution;

public interface IScriptExecutor
{
    Task<ExecutionResult> ExecuteAsync(
        Compilation.CompilationResult compilation,
        string typeName,
        string methodName,
        AutomationCallContext callContext,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default);

    Task<ExecutionResult<T>> ExecuteAsync<T>(
        Compilation.CompilationResult compilation,
        string typeName,
        string methodName,
        AutomationCallContext callContext,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default);

    Task CancelAsync(Guid executionId);
}

