using System.Collections.Concurrent;
using System.Reflection;
using Cascade.CodeGen.Compilation;
using Cascade.UIAutomation.Discovery;
using Cascade.Vision.Capture;
using Cascade.Vision.OCR;

namespace Cascade.CodeGen.Execution;

public sealed class SandboxedExecutor : IScriptExecutor
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningExecutions = new();

    public async Task<ExecutionResult> ExecuteAsync(
        CompilationResult compilation,
        string typeName,
        string methodName,
        AutomationCallContext callContext,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteInternalAsync<object?>(
            compilation,
            typeName,
            methodName,
            callContext,
            context,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<ExecutionResult<T>> ExecuteAsync<T>(
        CompilationResult compilation,
        string typeName,
        string methodName,
        AutomationCallContext callContext,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteInternalAsync<T>(
            compilation,
            typeName,
            methodName,
            callContext,
            context,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public Task CancelAsync(Guid executionId)
    {
        if (_runningExecutions.TryRemove(executionId, out var cts))
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    private async Task<ExecutionResult<T>> ExecuteInternalAsync<T>(
        CompilationResult compilation,
        string typeName,
        string methodName,
        AutomationCallContext callContext,
        ExecutionContext? context,
        CancellationToken cancellationToken)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        if (callContext is null)
        {
            throw new ArgumentNullException(nameof(callContext));
        }

        callContext.Session.EnsureValid();

        var execContext = context ?? new ExecutionContext();
        var executionId = execContext.ExecutionId;
        var assembly = compilation.Assembly ?? Assembly.Load(compilation.AssemblyBytes ?? Array.Empty<byte>());
        var type = assembly.GetType(typeName, throwOnError: true) ?? throw new InvalidOperationException($"Type '{typeName}' not found.");
        var instance = CreateInstance(type, callContext, execContext);
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

        if (method is null)
        {
            throw new InvalidOperationException($"Method '{methodName}' not found on '{typeName}'.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            callContext.Cancellation,
            cancellationToken,
            execContext.CancellationToken);

        _runningExecutions[executionId] = linkedCts;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var parameters = BuildParameters(method, execContext);
            var invocationResult = method.Invoke(instance, parameters);

            object? returnValue = null;

            if (invocationResult is Task task)
            {
                if (method.ReturnType.IsGenericType)
                {
                    await task.ConfigureAwait(false);
                    var resultProperty = task.GetType().GetProperty("Result");
                    returnValue = resultProperty?.GetValue(task);
                }
                else
                {
                    await task.ConfigureAwait(false);
                }
            }
            else
            {
                returnValue = invocationResult;
            }

            stopwatch.Stop();

            return new ExecutionResult<T>
            {
                Success = true,
                ExecutionId = executionId,
                ExecutionTime = stopwatch.Elapsed,
                ReturnValue = (T?)returnValue,
                Status = ExecutionStatus.Completed
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new ExecutionResult<T>
            {
                Success = false,
                ExecutionId = executionId,
                ExecutionTime = stopwatch.Elapsed,
                Status = ExecutionStatus.Cancelled
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            execContext.Log("Script execution failed.", ex);
            return new ExecutionResult<T>
            {
                Success = false,
                ExecutionId = executionId,
                ExecutionTime = stopwatch.Elapsed,
                Exception = ex,
                Status = ExecutionStatus.Failed
            };
        }
        finally
        {
            _runningExecutions.TryRemove(executionId, out _);
        }
    }

    private static object?[]? BuildParameters(MethodInfo method, ExecutionContext context)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return Array.Empty<object>();
        }

        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            args[i] = ResolveParameter(parameter, context);
        }

        return args;
    }

    private static object? ResolveParameter(ParameterInfo parameter, ExecutionContext context)
    {
        if (parameter.ParameterType == typeof(CancellationToken))
        {
            return context.CancellationToken;
        }

        if (parameter.ParameterType == typeof(ExecutionContext))
        {
            return context;
        }

        if (context.Services is not null)
        {
            var service = context.Services.GetService(parameter.ParameterType);
            if (service is not null)
            {
                return service;
            }
        }

        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        throw new InvalidOperationException($"Unable to resolve parameter '{parameter.Name}' of type '{parameter.ParameterType.FullName}'.");
    }

    private static object CreateInstance(Type type, AutomationCallContext callContext, ExecutionContext context)
    {
        var constructors = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var args = new object?[parameters.Length];
            var canUse = true;

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.ParameterType == typeof(AutomationCallContext))
                {
                    args[i] = callContext;
                }
                else if (parameter.ParameterType == typeof(IElementDiscovery))
                {
                    args[i] = context.ElementDiscovery ?? throw new InvalidOperationException("Element discovery service is not available.");
                }
                else if (parameter.ParameterType == typeof(IGeneratedActionExecutor))
                {
                    args[i] = context.ActionExecutor ?? throw new InvalidOperationException("Action executor service is not available.");
                }
                else if (parameter.ParameterType == typeof(IScreenCapture))
                {
                    args[i] = context.ScreenCapture ?? throw new InvalidOperationException("Screen capture service is not available.");
                }
                else if (parameter.ParameterType == typeof(IOcrEngine))
                {
                    args[i] = context.OcrEngine ?? throw new InvalidOperationException("OCR service is not available.");
                }
                else if (parameter.ParameterType == typeof(IServiceProvider))
                {
                    args[i] = context.Services ?? throw new InvalidOperationException("Service provider is not available.");
                }
                else if (context.Services?.GetService(parameter.ParameterType) is { } resolved)
                {
                    args[i] = resolved;
                }
                else if (parameter.HasDefaultValue)
                {
                    args[i] = parameter.DefaultValue;
                }
                else
                {
                    canUse = false;
                    break;
                }
            }

            if (canUse)
            {
                return constructor.Invoke(args);
            }
        }

        throw new InvalidOperationException($"Unable to create instance of '{type.FullName}'. No suitable constructor found.");
    }
}

