using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cascade.CodeGen.Compilation;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Generation;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Repositories;
using ScriptExecutionContext = Cascade.CodeGen.Execution.ExecutionContext;

namespace Cascade.CodeGen.Services;

public sealed class CodeGenService : ICodeGenService
{
    private readonly ICodeGenerator _codeGenerator;
    private readonly IScriptCompiler _compiler;
    private readonly IScriptExecutor _executor;
    private readonly IScriptRepository _scriptRepository;
    private readonly CodeGenOptions _options;
    private readonly ConcurrentDictionary<(Guid ScriptId, string Version), CompilationResult> _compilationCache = new();

    public CodeGenService(
        ICodeGenerator codeGenerator,
        IScriptCompiler compiler,
        IScriptExecutor executor,
        IScriptRepository scriptRepository,
        CodeGenOptions options)
    {
        _codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _scriptRepository = scriptRepository ?? throw new ArgumentNullException(nameof(scriptRepository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<GeneratedCode> GenerateActionAsync(ActionDefinition action, CancellationToken cancellationToken = default)
        => _codeGenerator.GenerateActionAsync(action, cancellationToken);

    public Task<GeneratedCode> GenerateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
        => _codeGenerator.GenerateWorkflowAsync(workflow, cancellationToken);

    public async Task<Script> SaveGeneratedScriptAsync(string name, string description, ScriptType type, GeneratedCode generatedCode, CancellationToken cancellationToken = default)
    {
        if (generatedCode is null)
        {
            throw new ArgumentNullException(nameof(generatedCode));
        }

        var script = new Script
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            SourceCode = generatedCode.SourceCode,
            Type = type,
            CurrentVersion = "1.0.0",
            TypeName = $"{generatedCode.Namespace}.{generatedCode.Metadata.Parameters.GetValueOrDefault("className", "GeneratedAction")}",
            MethodName = generatedCode.Metadata.Parameters.TryGetValue("entryMethod", out var entry)
                ? entry?.ToString()
                : "ExecuteAsync",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Metadata = generatedCode.Metadata.Parameters.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString() ?? string.Empty)
        };

        return await _scriptRepository.SaveAsync(script).ConfigureAwait(false);
    }

    public Task<CompilationResult> CompileAsync(GeneratedCode code, CancellationToken cancellationToken = default)
    {
        if (code is null)
        {
            throw new ArgumentNullException(nameof(code));
        }

        return _compiler.CompileAsync(code.SourceCode, cancellationToken: cancellationToken);
    }

    public async Task<ExecutionResult> ExecuteAsync(Guid scriptId, AutomationCallContext callContext, ScriptExecutionContext executionContext, CancellationToken cancellationToken = default)
    {
        var script = await _scriptRepository.GetByIdAsync(scriptId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Script '{scriptId}' was not found.");

        var compilation = await GetCompilationAsync(script, cancellationToken).ConfigureAwait(false);
        var typeName = script.TypeName ?? $"{_options.DefaultNamespace}.{script.Name}";
        var methodName = script.MethodName ?? "ExecuteAsync";

        return await _executor.ExecuteAsync(
            compilation,
            typeName,
            methodName,
            callContext,
            executionContext,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CompilationResult> GetCompilationAsync(Script script, CancellationToken cancellationToken)
    {
        var cacheKey = (script.Id, script.CurrentVersion);
        if (_options.CacheCompilations && _compilationCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var cachedBytes = await _scriptRepository.GetCompiledAssemblyAsync(script.Id, script.CurrentVersion).ConfigureAwait(false);
        if (cachedBytes is not null)
        {
            var cachedResult = new CompilationResult
            {
                Success = true,
                AssemblyBytes = cachedBytes
            };

            if (_options.CacheCompilations)
            {
                _compilationCache[cacheKey] = cachedResult;
                TrimCacheIfNeeded();
            }

            return cachedResult;
        }

        var compilation = await _compiler.CompileAsync(script.SourceCode, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!compilation.Success)
        {
            throw new InvalidOperationException($"Compilation failed: {string.Join(Environment.NewLine, compilation.Errors.Select(e => e.Message))}");
        }

        if (compilation.AssemblyBytes is not null)
        {
            await _scriptRepository.SaveCompiledAssemblyAsync(script.Id, script.CurrentVersion, compilation.AssemblyBytes)
                .ConfigureAwait(false);
        }

        if (_options.CacheCompilations)
        {
            _compilationCache[cacheKey] = compilation;
            TrimCacheIfNeeded();
        }

        return compilation;
    }

    private void TrimCacheIfNeeded()
    {
        if (_compilationCache.Count <= _options.MaxCachedCompilations)
        {
            return;
        }

        foreach (var key in _compilationCache.Keys.Take(_compilationCache.Count - _options.MaxCachedCompilations))
        {
            _compilationCache.TryRemove(key, out _);
        }
    }
}

