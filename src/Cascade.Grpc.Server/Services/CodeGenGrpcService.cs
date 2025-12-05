using System.Text.Json;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Services;
using Cascade.Core.Session;
using Cascade.Database.Enums;
using Cascade.Database.Filters;
using Cascade.Database.Repositories;
using Cascade.Grpc.CodeGen;
using Cascade.Grpc.Server.Mappers;
using Cascade.Grpc.Server.Sessions;
using Cascade.UIAutomation.Session;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoCodeGenService = Cascade.Grpc.CodeGen.CodeGenService;
using DomainWorkflowStep = Cascade.CodeGen.Generation.WorkflowStep;
using ScriptEntity = Cascade.Database.Entities.Script;
using ProtoScript = Cascade.Grpc.CodeGen.Script;

namespace Cascade.Grpc.Server.Services;

public sealed class CodeGenGrpcService : ProtoCodeGenService.CodeGenServiceBase
{
    private readonly ICodeGenService _codeGenService;
    private readonly IScriptRepository _scriptRepository;
    private readonly ISessionRuntimeResolver _runtimeResolver;
    private readonly IGrpcSessionContextAccessor _sessionAccessor;
    private readonly ILogger<CodeGenGrpcService> _logger;

    public CodeGenGrpcService(
        ICodeGenService codeGenService,
        IScriptRepository scriptRepository,
        ISessionRuntimeResolver runtimeResolver,
        IGrpcSessionContextAccessor sessionAccessor,
        ILogger<CodeGenGrpcService> logger)
    {
        _codeGenService = codeGenService ?? throw new ArgumentNullException(nameof(codeGenService));
        _scriptRepository = scriptRepository ?? throw new ArgumentNullException(nameof(scriptRepository));
        _runtimeResolver = runtimeResolver ?? throw new ArgumentNullException(nameof(runtimeResolver));
        _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));
        _logger = logger;
    }

    public override async Task<GeneratedCodeResponse> GenerateAction(GenerateActionRequest request, ServerCallContext context)
    {
        var action = new ActionDefinition
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "GeneratedAction" : request.Name,
            Type = ParseActionType(request.ActionType),
            TargetElement = ParseLocator(request.ElementLocator),
            Parameters = request.Parameters.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
        };

        var generated = await _codeGenService.GenerateActionAsync(action, context.CancellationToken).ConfigureAwait(false);
        return generated.ToProto();
    }

    public override async Task<GeneratedCodeResponse> GenerateWorkflow(GenerateWorkflowRequest request, ServerCallContext context)
    {
        var workflow = new WorkflowDefinition
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "GeneratedWorkflow" : request.Name,
            Description = request.Description,
            Steps = request.Steps.Select(step => new DomainWorkflowStep
            {
                Order = step.Order,
                Name = string.IsNullOrWhiteSpace(step.Name) ? $"Step{step.Order}" : step.Name,
                Action = new ActionDefinition
                {
                    Name = step.Name,
                    Type = ParseActionType(step.ActionType),
                    TargetElement = ParseLocator(step.ElementLocator),
                    Parameters = step.Parameters.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                },
                DelayAfter = step.DelayAfterMs > 0 ? TimeSpan.FromMilliseconds(step.DelayAfterMs) : null
            }).ToList()
        };

        var generated = await _codeGenService.GenerateWorkflowAsync(workflow, context.CancellationToken).ConfigureAwait(false);
        return generated.ToProto();
    }

    public override Task<GeneratedCodeResponse> GenerateAgent(GenerateAgentRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Agent code generation is not implemented yet."));
    }

    public override async Task<CompileResponse> Compile(CompileRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "source_code is required."));
        }

        var generated = new GeneratedCode { SourceCode = request.SourceCode };
        var result = await _codeGenService.CompileAsync(generated, context.CancellationToken).ConfigureAwait(false);
        return result.ToProto();
    }

    public override async Task<SyntaxCheckResponse> CheckSyntax(CheckSyntaxRequest request, ServerCallContext context)
    {
        var compileResponse = await Compile(new CompileRequest { SourceCode = request.SourceCode }, context).ConfigureAwait(false);
        return new SyntaxCheckResponse
        {
            Result = compileResponse.Result,
            IsValid = compileResponse.CompilationSuccess,
            Diagnostics = { compileResponse.Errors }
        };
    }

    public override Task<ExecuteResponse> Execute(ExecuteRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Script execution over gRPC is not yet implemented."));
    }

    public override Task<ExecuteResponse> ExecuteScript(ExecuteScriptRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Ad-hoc script execution is not yet implemented."));
    }

    public override async Task<ScriptResponse> SaveScript(SaveScriptRequest request, ServerCallContext context)
    {
        ValidateString(request.Name, "name");
        ValidateString(request.SourceCode, "source_code");

        var script = new ScriptEntity
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid() : ParseGuid(request.Id, "id"),
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            SourceCode = request.SourceCode,
            Type = ParseScriptType(request.Type),
            Metadata = request.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await _scriptRepository.SaveAsync(script).ConfigureAwait(false);
        return saved.ToProto();
    }

    public override async Task<ScriptResponse> GetScript(GetScriptRequest request, ServerCallContext context)
    {
        ScriptEntity? script = null;
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            script = await _scriptRepository.GetByIdAsync(ParseGuid(request.Id, "id")).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(request.Name))
        {
            script = await _scriptRepository.GetByNameAsync(request.Name).ConfigureAwait(false);
        }

        if (script is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Script not found."));
        }

        return script.ToProto();
    }

    public override async Task<ScriptListResponse> ListScripts(ListScriptsRequest request, ServerCallContext context)
    {
        var filter = new ScriptFilter
        {
            Type = ParseScriptFilterType(request.Type),
            Name = request.NameContains,
            Skip = request.Offset,
            Take = request.Limit > 0 ? request.Limit : 0
        };

        var scripts = await _scriptRepository.GetAllAsync(filter).ConfigureAwait(false);
        var response = new ScriptListResponse
        {
            Result = ProtoResults.Success(),
            TotalCount = scripts.Count
        };

        response.Scripts.AddRange(scripts.Select(s => s.ToProtoScript()));
        return response;
    }

    public override async Task<Result> DeleteScript(DeleteScriptRequest request, ServerCallContext context)
    {
        var id = ParseGuid(request.Id, "id");
        await _scriptRepository.DeleteAsync(id).ConfigureAwait(false);
        return ProtoResults.Success();
    }

    private async Task<SessionHandle> ResolveSessionHandleAsync(ServerCallContext context)
    {
        var session = _sessionAccessor.Current ?? new GrpcSessionContext("local", "local-agent", "local-run");
        var runtime = await _runtimeResolver.ResolveAsync(session, context.CancellationToken).ConfigureAwait(false);
        return runtime.Handle;
    }

    private static ActionType ParseActionType(string value)
    {
        if (Enum.TryParse<ActionType>(value, true, out var result))
        {
            return result;
        }

        return ActionType.Click;
    }

    private static ScriptType ParseScriptType(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ScriptType.Utility
            : value.ToDomain();
    }

    private static ScriptType? ParseScriptFilterType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.ToDomain();
    }

    private static ElementLocator ParseLocator(string locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
        {
            return new ElementLocator();
        }

        try
        {
            return JsonSerializer.Deserialize<ElementLocator>(locator) ?? new ElementLocator { AutomationId = locator };
        }
        catch
        {
            return new ElementLocator { AutomationId = locator };
        }
    }

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be a valid GUID."));
        }

        return guid;
    }

    private static void ValidateString(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} is required."));
        }
    }
}

