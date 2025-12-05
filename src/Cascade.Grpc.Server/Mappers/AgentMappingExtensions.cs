using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Google.Protobuf.Collections;
using System.Linq;
using AgentMessage = Cascade.Grpc.Agent.Agent;
using AgentResponseMessage = Cascade.Grpc.Agent.AgentResponse;
using AgentVersionMessage = Cascade.Grpc.Agent.AgentVersion;
using AgentVersionResponseMessage = Cascade.Grpc.Agent.AgentVersionResponse;
using ScriptMessage = Cascade.Grpc.Agent.Script;
using ProtoExecutionRecord = Cascade.Grpc.Agent.ExecutionRecord;
using ProtoExecutionHistoryResponse = Cascade.Grpc.Agent.ExecutionHistoryResponse;
using ProtoExecutionStep = Cascade.Grpc.Agent.ExecutionStep;
using CreateAgentRequestProto = Cascade.Grpc.Agent.CreateAgentRequest;
using UpdateAgentRequestProto = Cascade.Grpc.Agent.UpdateAgentRequest;
using AgentEntity = Cascade.Database.Entities.Agent;
using ExecutionRecordEntity = Cascade.Database.Entities.ExecutionRecord;
using AgentVersionEntity = Cascade.Database.Entities.AgentVersion;


namespace Cascade.Grpc.Server.Mappers;

internal static class AgentMappingExtensions
{
    public static AgentResponseMessage ToProto(this AgentEntity agentEntity)
    {
        return new AgentResponseMessage
        {
            Result = ProtoResults.Success(),
            Agent = agentEntity.ToProtoAgent()
        };
    }

    public static AgentMessage ToProtoAgent(this AgentEntity agentEntity)
    {
        var agent = new AgentMessage
        {
            Id = agentEntity.Id.ToString(),
            Name = agentEntity.Name,
            Description = agentEntity.Description,
            TargetApplication = agentEntity.TargetApplication,
            ActiveVersion = agentEntity.ActiveVersion,
            CreatedAt = agentEntity.CreatedAt.ToString("O"),
            UpdatedAt = agentEntity.UpdatedAt.ToString("O")
        };

        agent.Capabilities.Add(agentEntity.Capabilities);
        foreach (var kvp in agentEntity.Metadata)
        {
            agent.Metadata.Add(kvp.Key, kvp.Value);
        }

        return agent;
    }

    public static AgentVersionResponseMessage ToProto(this AgentVersionEntity version)
    {
        return new AgentVersionResponseMessage
        {
            Result = ProtoResults.Success(),
            Version = new AgentVersionMessage
            {
                Version = version.Version,
                CreatedAt = version.CreatedAt.ToString("O"),
                Notes = version.Notes ?? string.Empty,
                IsActive = version.IsActive
            }
        };
    }

    public static AgentVersionMessage ToProtoVersion(this AgentVersionEntity version)
    {
        return new AgentVersionMessage
        {
            Version = version.Version,
            CreatedAt = version.CreatedAt.ToString("O"),
            Notes = version.Notes ?? string.Empty,
            IsActive = version.IsActive
        };
    }

    public static ProtoExecutionHistoryResponse ToProto(this IReadOnlyList<ExecutionRecord> records)
    {
        var response = new ProtoExecutionHistoryResponse
        {
            Result = ProtoResults.Success()
        };

        foreach (var record in records)
        {
            response.Records.Add(record.ToProto());
        }

        return response;
    }

    public static ProtoExecutionRecord ToProto(this ExecutionRecordEntity record)
    {
        var proto = new ProtoExecutionRecord
        {
            Id = record.Id.ToString(),
            AgentId = record.AgentId.ToString(),
            TaskDescription = record.TaskDescription ?? string.Empty,
            Success = record.Success,
            ErrorMessage = record.ErrorMessage ?? string.Empty,
            DurationMs = record.DurationMs,
            ExecutedAt = record.CompletedAt.ToString("O")
        };

        foreach (var step in record.Steps.OrderBy(s => s.Order))
        {
            proto.Steps.Add(new ProtoExecutionStep
            {
                Order = step.Order,
                Action = step.Action,
                Success = step.Success,
                Error = step.Error ?? string.Empty,
                DurationMs = step.DurationMs
            });
        }

        return proto;
    }

    public static AgentEntity ToEntity(this CreateAgentRequestProto request)
    {
        return new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            TargetApplication = request.TargetApplication,
            Capabilities = request.Capabilities.ToList(),
            InstructionList = request.InstructionList,
            Metadata = request.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static void ApplyUpdates(this AgentEntity agent, UpdateAgentRequestProto request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            agent.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            agent.Description = request.Description;
        }

        if (request.Capabilities.Count > 0)
        {
            agent.Capabilities = request.Capabilities.ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.InstructionList))
        {
            agent.InstructionList = request.InstructionList;
        }

        if (request.Metadata.Count > 0)
        {
            foreach (var kvp in request.Metadata)
            {
                agent.Metadata[kvp.Key] = kvp.Value;
            }
        }

        agent.UpdatedAt = DateTime.UtcNow;
    }
}

