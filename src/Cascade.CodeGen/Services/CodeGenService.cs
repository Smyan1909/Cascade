using Cascade.CodeGen.Compiler;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Persistence;
using Cascade.CodeGen.Recording;
using Cascade.CodeGen.Templates;
using Cascade.Database.Repositories;
using Cascade.Database.Entities;
using Cascade.Database.Enums;

namespace Cascade.CodeGen.Services;

/// <summary>
/// Main service facade for code generation functionality.
/// </summary>
public class CodeGenService
{
    private readonly ITemplateEngine _templateEngine;
    private readonly IScriptCompiler _compiler;
    private readonly IScriptExecutor _executor;
    private readonly ActionCodeGenerator _actionGenerator;
    private readonly WorkflowGenerator _workflowGenerator;
    private readonly AgentCodeGenerator _agentGenerator;
    private readonly IActionRecorder _recorder;
    private readonly IScriptRepository? _scriptRepository;
    private readonly CodeGenOptions _options;

    /// <summary>
    /// Creates a new CodeGen service.
    /// </summary>
    public CodeGenService(
        CodeGenOptions? options = null,
        ITemplateEngine? templateEngine = null,
        IScriptCompiler? compiler = null,
        IScriptExecutor? executor = null,
        IScriptRepository? scriptRepository = null,
        IActionRecorder? recorder = null)
    {
        _options = options ?? new CodeGenOptions();
        _templateEngine = templateEngine ?? new ScribanTemplateEngine();
        _compiler = compiler ?? new RoslynCompiler();
        _executor = executor ?? new SandboxedExecutor(_compiler);
        _scriptRepository = scriptRepository;
        _recorder = recorder ?? new ActionRecorder();

        _actionGenerator = new ActionCodeGenerator(_templateEngine, _options.DefaultNamespace);
        _workflowGenerator = new WorkflowGenerator(_templateEngine, _options.DefaultNamespace);
        _agentGenerator = new AgentCodeGenerator(_templateEngine, _options.DefaultNamespace);
    }

    /// <summary>
    /// Generates and compiles code from an action definition.
    /// </summary>
    public async Task<CompilationResult> GenerateAndCompileActionAsync(ActionDefinition action, CompilationOptions? compilationOptions = null)
    {
        var generatedCode = await _actionGenerator.GenerateActionAsync(action);
        if (_options.OptimizeGeneratedCode)
        {
            generatedCode.SourceCode = await _actionGenerator.OptimizeAsync(generatedCode.SourceCode);
        }

        var options = compilationOptions ?? _options.DefaultCompilationOptions;
        return await _compiler.CompileAsync(generatedCode.SourceCode, options);
    }

    /// <summary>
    /// Generates and compiles code from a workflow definition.
    /// </summary>
    public async Task<CompilationResult> GenerateAndCompileWorkflowAsync(WorkflowDefinition workflow, CompilationOptions? compilationOptions = null)
    {
        var generatedCode = await _workflowGenerator.GenerateWorkflowAsync(workflow);
        if (_options.OptimizeGeneratedCode)
        {
            generatedCode.SourceCode = await _workflowGenerator.OptimizeAsync(generatedCode.SourceCode);
        }

        var options = compilationOptions ?? _options.DefaultCompilationOptions;
        return await _compiler.CompileAsync(generatedCode.SourceCode, options);
    }

    /// <summary>
    /// Generates and compiles code from an agent definition.
    /// </summary>
    public async Task<CompilationResult> GenerateAndCompileAgentAsync(AgentDefinition agent, CompilationOptions? compilationOptions = null)
    {
        var generatedCode = await _agentGenerator.GenerateAgentAsync(agent);
        if (_options.OptimizeGeneratedCode)
        {
            generatedCode.SourceCode = await _agentGenerator.OptimizeAsync(generatedCode.SourceCode);
        }

        var options = compilationOptions ?? _options.DefaultCompilationOptions;
        return await _compiler.CompileAsync(generatedCode.SourceCode, options);
    }

    /// <summary>
    /// Saves a generated script to the database.
    /// </summary>
    public async Task<Script> SaveScriptAsync(GeneratedCode generatedCode, string name, string description, ScriptType type, Guid? agentId = null)
    {
        if (_scriptRepository == null)
            throw new InvalidOperationException("Script repository not configured");

        var script = generatedCode.ToScript(name, description, type, agentId);
        return await _scriptRepository.SaveAsync(script);
    }

    /// <summary>
    /// Loads and executes a script from the database.
    /// </summary>
    public async Task<ExecutionResult> LoadAndExecuteScriptAsync(Guid scriptId, CodeGen.Execution.ExecutionContext? context = null)
    {
        if (_scriptRepository == null)
            throw new InvalidOperationException("Script repository not configured");

        var script = await _scriptRepository.GetByIdAsync(scriptId);
        if (script == null)
            throw new InvalidOperationException($"Script {scriptId} not found");

        context ??= new CodeGen.Execution.ExecutionContext
        {
            Timeout = _options.DefaultTimeout,
            SecurityPolicy = _options.DefaultSecurityPolicy
        };

        // Compile if needed
        CompilationResult compilation;
        if (script.CompiledAssembly != null && script.LastCompiledAt.HasValue)
        {
            // Load existing assembly
            compilation = new CompilationResult
            {
                Success = true,
                AssemblyBytes = script.CompiledAssembly,
                Assembly = System.Reflection.Assembly.Load(script.CompiledAssembly)
            };
        }
        else
        {
            // Compile on the fly
            compilation = await _compiler.CompileAsync(script.SourceCode, _options.DefaultCompilationOptions);
        }

        if (!compilation.Success)
            throw new InvalidOperationException($"Script compilation failed: {string.Join(", ", compilation.Errors.Select(e => e.Message))}");

        var typeName = script.TypeName ?? throw new InvalidOperationException("Script TypeName not set");
        var methodName = script.MethodName ?? "ExecuteAsync";

        return await _executor.ExecuteAsync(compilation, typeName, methodName, context);
    }

    /// <summary>
    /// Starts a recording session.
    /// </summary>
    public Task<RecordingSession> StartRecordingAsync(RecordingOptions? options = null)
    {
        return _recorder.StartRecordingAsync(options);
    }

    /// <summary>
    /// Stops a recording session.
    /// </summary>
    public Task StopRecordingAsync(Guid sessionId)
    {
        return _recorder.StopRecordingAsync(sessionId);
    }

    /// <summary>
    /// Records an action manually.
    /// </summary>
    public Task RecordActionAsync(Guid sessionId, RecordedAction action)
    {
        return _recorder.RecordActionAsync(sessionId, action);
    }
}

