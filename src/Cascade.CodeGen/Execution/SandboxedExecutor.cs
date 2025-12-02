using System.Reflection;
using Cascade.CodeGen.Compiler;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Cascade.CodeGen.Execution;

/// <summary>
/// Executes scripts in a sandboxed environment with timeout and cancellation support.
/// </summary>
public class SandboxedExecutor : IScriptExecutor
{
    private readonly Dictionary<Guid, CancellationTokenSource> _activeExecutions = new();
    private readonly IScriptCompiler _compiler;

    /// <summary>
    /// Creates a new sandboxed executor.
    /// </summary>
    public SandboxedExecutor(IScriptCompiler? compiler = null)
    {
        _compiler = compiler ?? new RoslynCompiler();
    }

    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteAsync(
        CompilationResult compilation,
        string typeName,
        string methodName,
        ExecutionContext? context = null)
    {
        if (!compilation.Success || compilation.Assembly == null)
        {
            return new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Failed,
                Exception = new InvalidOperationException("Compilation was not successful")
            };
        }

        context ??= new ExecutionContext();
        var startTime = DateTime.UtcNow;
        var logs = new List<string>();

        try
        {
            using var cts = CreateCancellationTokenSource(context);
            _activeExecutions[context.ExecutionId] = cts;

            // Create instance of the type
            var type = compilation.Assembly.GetType(typeName);
            if (type == null)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Status = ExecutionStatus.Failed,
                    Exception = new InvalidOperationException($"Type '{typeName}' not found in compiled assembly"),
                    ExecutionId = context.ExecutionId,
                    ExecutionTime = DateTime.UtcNow - startTime,
                    Logs = logs
                };
            }

            var instance = Activator.CreateInstance(type);
            if (instance == null)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Status = ExecutionStatus.Failed,
                    Exception = new InvalidOperationException($"Failed to create instance of type '{typeName}'"),
                    ExecutionId = context.ExecutionId,
                    ExecutionTime = DateTime.UtcNow - startTime,
                    Logs = logs
                };
            }

            // Get the method
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (method == null)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Status = ExecutionStatus.Failed,
                    Exception = new InvalidOperationException($"Method '{methodName}' not found in type '{typeName}'"),
                    ExecutionId = context.ExecutionId,
                    ExecutionTime = DateTime.UtcNow - startTime,
                    Logs = logs
                };
            }

            // Execute with timeout
            var executionTask = ExecuteMethodAsync(instance, method, context, logs, cts.Token);
            var timeoutTask = Task.Delay(context.Timeout, cts.Token);

            var completedTask = await Task.WhenAny(executionTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                cts.Cancel();
                return new ExecutionResult
                {
                    Success = false,
                    Status = ExecutionStatus.Timeout,
                    Exception = new TimeoutException($"Execution exceeded timeout of {context.Timeout}"),
                    ExecutionId = context.ExecutionId,
                    ExecutionTime = DateTime.UtcNow - startTime,
                    Logs = logs
                };
            }

            var result = await executionTask;
            result.ExecutionId = context.ExecutionId;
            result.ExecutionTime = DateTime.UtcNow - startTime;
            result.Logs = logs;
            return result;
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Cancelled,
                Exception = new OperationCanceledException("Execution was cancelled"),
                ExecutionId = context.ExecutionId,
                ExecutionTime = DateTime.UtcNow - startTime,
                Logs = logs
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Failed,
                Exception = ex,
                ExecutionId = context.ExecutionId,
                ExecutionTime = DateTime.UtcNow - startTime,
                Logs = logs
            };
        }
        finally
        {
            _activeExecutions.Remove(context.ExecutionId);
        }
    }

    /// <inheritdoc />
    public async Task<ExecutionResult<T>> ExecuteAsync<T>(
        CompilationResult compilation,
        string typeName,
        string methodName,
        object?[]? parameters = null,
        ExecutionContext? context = null)
    {
        var result = await ExecuteAsync(compilation, typeName, methodName, context);
        
        var typedResult = new ExecutionResult<T>
        {
            Success = result.Success,
            Status = result.Status,
            Exception = result.Exception,
            ExecutionTime = result.ExecutionTime,
            ExecutionId = result.ExecutionId,
            Logs = result.Logs
        };

        if (result.Success && result.ReturnValue is T typedValue)
        {
            typedResult.ReturnValue = typedValue;
        }

        return typedResult;
    }

    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteScriptAsync(string script, ExecutionContext? context = null)
    {
        context ??= new ExecutionContext();
        var startTime = DateTime.UtcNow;
        var logs = new List<string>();

        try
        {
            using var cts = CreateCancellationTokenSource(context);
            _activeExecutions[context.ExecutionId] = cts;

            var globals = new ScriptGlobals { Variables = context.Variables };
            var scriptResult = await _compiler.EvaluateAsync(script, globals);

            return new ExecutionResult
            {
                Success = scriptResult.Success,
                Status = scriptResult.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                ReturnValue = scriptResult.ReturnValue,
                Exception = scriptResult.Exception,
                ExecutionId = context.ExecutionId,
                ExecutionTime = DateTime.UtcNow - startTime,
                Logs = logs
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Cancelled,
                Exception = new OperationCanceledException("Execution was cancelled"),
                ExecutionId = context.ExecutionId,
                ExecutionTime = DateTime.UtcNow - startTime,
                Logs = logs
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Failed,
                Exception = ex,
                ExecutionId = context.ExecutionId,
                ExecutionTime = DateTime.UtcNow - startTime,
                Logs = logs
            };
        }
        finally
        {
            _activeExecutions.Remove(context.ExecutionId);
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(Guid executionId)
    {
        if (_activeExecutions.TryGetValue(executionId, out var cts))
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    private async Task<ExecutionResult> ExecuteMethodAsync(
        object instance,
        MethodInfo method,
        ExecutionContext context,
        List<string> logs,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Log execution start
            context.LogInfo?.Invoke($"Executing method {method.Name}");
            logs.Add($"Executing method {method.Name}");

            object? result = null;
            
            // Check if method is async
            if (method.ReturnType.IsAssignableTo(typeof(Task)))
            {
                var task = method.Invoke(instance, null) as Task;
                if (task != null)
                {
                    await task.ConfigureAwait(false);
                    
                    // Get result if it's Task<T>
                    if (method.ReturnType.IsGenericType)
                    {
                        var resultProperty = method.ReturnType.GetProperty("Result");
                        result = resultProperty?.GetValue(task);
                    }
                }
            }
            else
            {
                result = method.Invoke(instance, null);
            }

            return new ExecutionResult
            {
                Success = true,
                Status = ExecutionStatus.Completed,
                ReturnValue = result
            };
        }
        catch (Exception ex)
        {
            context.LogError?.Invoke($"Error executing method {method.Name}", ex);
            logs.Add($"Error: {ex.Message}");
            
            return new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Failed,
                Exception = ex
            };
        }
    }

    private CancellationTokenSource CreateCancellationTokenSource(ExecutionContext context)
    {
        var cts = new CancellationTokenSource();
        
        // Combine context cancellation token with timeout
        if (context.CancellationToken != CancellationToken.None)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        }

        // Add timeout
        if (context.Timeout != Timeout.InfiniteTimeSpan)
        {
            cts.CancelAfter(context.Timeout);
        }

        return cts;
    }
}

