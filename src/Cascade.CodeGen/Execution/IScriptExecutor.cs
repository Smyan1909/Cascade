using Cascade.CodeGen.Compiler;

namespace Cascade.CodeGen.Execution;

/// <summary>
/// Interface for executing compiled scripts.
/// </summary>
public interface IScriptExecutor
{
    /// <summary>
    /// Executes a method from a compiled assembly.
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(
        CompilationResult compilation,
        string typeName,
        string methodName,
        ExecutionContext? context = null);

    /// <summary>
    /// Executes a method from a compiled assembly with parameters.
    /// </summary>
    Task<ExecutionResult<T>> ExecuteAsync<T>(
        CompilationResult compilation,
        string typeName,
        string methodName,
        object?[]? parameters = null,
        ExecutionContext? context = null);

    /// <summary>
    /// Executes inline script code.
    /// </summary>
    Task<ExecutionResult> ExecuteScriptAsync(
        string script,
        ExecutionContext? context = null);

    /// <summary>
    /// Cancels an execution by ID.
    /// </summary>
    Task CancelAsync(Guid executionId);
}

