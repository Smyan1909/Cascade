using Cascade.Database.Enums;
using Cascade.Database.Filters;
using Cascade.Database.Repositories;
using Cascade.Grpc.Agent;
using Cascade.Grpc.Server.Mappers;
using Grpc.Core;
using System.Linq;
using Microsoft.Extensions.Logging;
using ScriptMessage = Cascade.Grpc.Agent.Script;
using AgentEntity = Cascade.Database.Entities.Agent;
using ExecutionRecordEntity = Cascade.Database.Entities.ExecutionRecord;
using ExecutionStepEntity = Cascade.Database.Entities.ExecutionStep;

namespace Cascade.Grpc.Server.Services;

public sealed class AgentGrpcService : AgentService.AgentServiceBase
{
    private readonly IAgentRepository _agentRepository;
    private readonly IScriptRepository _scriptRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly ILogger<AgentGrpcService> _logger;

    public AgentGrpcService(
        IAgentRepository agentRepository,
        IScriptRepository scriptRepository,
        IExecutionRepository executionRepository,
        ILogger<AgentGrpcService> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _scriptRepository = scriptRepository ?? throw new ArgumentNullException(nameof(scriptRepository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _logger = logger;
    }

    public override async Task<AgentResponse> CreateAgent(CreateAgentRequest request, ServerCallContext context)
    {
        ValidateString(request.Name, "name");
        ValidateString(request.TargetApplication, "target_application");

        var entity = request.ToEntity();
        var created = await _agentRepository.CreateAsync(entity).ConfigureAwait(false);
        return created.ToProto();
    }

    public override async Task<AgentResponse> GetAgent(GetAgentRequest request, ServerCallContext context)
    {
        var agent = await ResolveAgentAsync(request.Id, request.Name).ConfigureAwait(false);
        return agent.ToProto();
    }

    public override async Task<AgentResponse> UpdateAgent(UpdateAgentRequest request, ServerCallContext context)
    {
        var agent = await ResolveAgentAsync(request.Id, null).ConfigureAwait(false);
        agent.ApplyUpdates(request);
        var updated = await _agentRepository.UpdateAsync(agent).ConfigureAwait(false);
        return updated.ToProto();
    }

    public override async Task<Result> DeleteAgent(DeleteAgentRequest request, ServerCallContext context)
    {
        var agentId = ParseGuid(request.Id, "id");
        await _agentRepository.DeleteAsync(agentId).ConfigureAwait(false);
        return ProtoResults.Success();
    }

    public override async Task<AgentListResponse> ListAgents(ListAgentsRequest request, ServerCallContext context)
    {
        var filter = new AgentFilter
        {
            TargetApplication = request.TargetApplication,
            Name = request.NameContains,
            Skip = request.Offset,
            Take = request.Limit > 0 ? request.Limit : 0
        };

        var agents = await _agentRepository.GetAllAsync(filter).ConfigureAwait(false);
        var response = new AgentListResponse
        {
            Result = ProtoResults.Success(),
            TotalCount = agents.Count
        };

        response.Agents.AddRange(agents.Select(a => a.ToProtoAgent()));
        return response;
    }

    public override async Task<AgentVersionResponse> CreateVersion(CreateVersionRequest request, ServerCallContext context)
    {
        var agentId = ParseGuid(request.AgentId, "agent_id");
        var version = await _agentRepository.CreateVersionAsync(agentId, request.VersionNotes ?? string.Empty).ConfigureAwait(false);
        return version.ToProto();
    }

    public override async Task<AgentVersionListResponse> GetVersions(GetVersionsRequest request, ServerCallContext context)
    {
        var agentId = ParseGuid(request.AgentId, "agent_id");
        var versions = await _agentRepository.GetVersionsAsync(agentId).ConfigureAwait(false);
        var response = new AgentVersionListResponse
        {
            Result = ProtoResults.Success()
        };

        response.Versions.AddRange(versions.Select(v => v.ToProtoVersion()));
        return response;
    }

    public override async Task<Result> SetActiveVersion(SetActiveVersionRequest request, ServerCallContext context)
    {
        var agentId = ParseGuid(request.AgentId, "agent_id");
        ValidateString(request.Version, "version");
        await _agentRepository.SetActiveVersionAsync(agentId, request.Version).ConfigureAwait(false);
        return ProtoResults.Success();
    }

    public override async Task<AgentDefinitionResponse> GetAgentDefinition(GetAgentRequest request, ServerCallContext context)
    {
        var agent = await ResolveAgentAsync(request.Id, request.Name).ConfigureAwait(false);
        var scripts = await _scriptRepository.GetByAgentIdAsync(agent.Id).ConfigureAwait(false);

        var response = new AgentDefinitionResponse
        {
            Result = ProtoResults.Success(),
            InstructionList = agent.InstructionList
        };

        response.Capabilities.Add(agent.Capabilities);
        foreach (var script in scripts)
        {
            response.Scripts.Add(new ScriptMessage
            {
                Id = script.Id.ToString(),
                Name = script.Name,
                SourceCode = script.SourceCode
            });
        }

        return response;
    }

    public override async Task<Result> RecordExecution(RecordExecutionRequest request, ServerCallContext context)
    {
        var agentId = ParseGuid(request.AgentId, "agent_id");
        var now = DateTime.UtcNow;
        var record = new ExecutionRecordEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            TaskDescription = request.TaskDescription,
            Success = request.Success,
            ErrorMessage = request.ErrorMessage,
            DurationMs = request.DurationMs,
            StartedAt = now,
            CompletedAt = now
        };

        record.Steps = request.Steps.Select(step => new ExecutionStepEntity
        {
            Id = Guid.NewGuid(),
            ExecutionId = record.Id,
            Order = step.Order,
            Action = step.Action,
            Success = step.Success,
            Error = step.Error,
            DurationMs = step.DurationMs
        }).ToList<ExecutionStepEntity>();

        await _executionRepository.RecordExecutionAsync(record).ConfigureAwait(false);
        return ProtoResults.Success();
    }

    public override async Task<ExecutionHistoryResponse> GetExecutionHistory(GetExecutionHistoryRequest request, ServerCallContext context)
    {
        var agentId = ParseGuid(request.AgentId, "agent_id");
        var records = await _executionRepository.GetHistoryAsync(agentId, request.Limit, request.Offset).ConfigureAwait(false);
        return records.ToProto();
    }

    private async Task<AgentEntity> ResolveAgentAsync(string id, string? name)
    {
        AgentEntity? agent = null;
        if (!string.IsNullOrWhiteSpace(id))
        {
            var agentId = ParseGuid(id, "id");
            agent = await _agentRepository.GetByIdAsync(agentId).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            agent = await _agentRepository.GetByNameAsync(name).ConfigureAwait(false);
        }

        if (agent is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Agent not found."));
        }

        return agent;
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

