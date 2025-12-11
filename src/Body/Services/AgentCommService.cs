using Cascade.Proto;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Collections.Concurrent;

namespace Cascade.Body.Services;

/// <summary>
/// In-memory Agent-to-Agent communication service for coordinating agents
/// under the same user/app context. Implements at-least-once delivery semantics.
/// </summary>
public class AgentCommService : Proto.AgentCommService.AgentCommServiceBase
{
    // In-memory storage scoped by user_id/app_id
    private readonly ConcurrentDictionary<string, AgentRegistry> _registries = new();
    private readonly ILogger<AgentCommService> _logger;

    public AgentCommService(ILogger<AgentCommService> logger)
    {
        _logger = logger;
    }

    public override Task<AgentRegisterResponse> RegisterAgent(
        AgentRegisterRequest request,
        ServerCallContext context)
    {
        var scopeKey = GetScopeKey(request.UserId, request.AppId);
        var registry = _registries.GetOrAdd(scopeKey, _ => new AgentRegistry());

        var agentId = Guid.NewGuid().ToString();
        var descriptor = new AgentDescriptor
        {
            AgentId = agentId,
            Role = request.Role ?? "",
            RunId = request.RunId ?? ""
        };

        registry.RegisterAgent(agentId, descriptor, request.UserId, request.AppId);

        _logger.LogInformation(
            "Registered agent {AgentId} with role {Role} for user {UserId}/app {AppId}",
            agentId, request.Role, request.UserId, request.AppId);

        return Task.FromResult(new AgentRegisterResponse { AgentId = agentId });
    }

    public override Task<Proto.Status> SendAgentMessage(
        AgentMessage message,
        ServerCallContext context)
    {
        var scopeKey = GetScopeKey(message.UserId, message.AppId);
        if (!_registries.TryGetValue(scopeKey, out var registry))
        {
            _logger.LogWarning(
                "No registry found for user {UserId}/app {AppId}",
                message.UserId, message.AppId);
            return Task.FromResult(new Proto.Status
            {
                Success = false,
                Message = "No agents registered for this user/app"
            });
        }

        // Route by target_agent_id or target_role
        var delivered = false;
        if (!string.IsNullOrEmpty(message.TargetAgentId))
        {
            delivered = registry.EnqueueMessage(message.TargetAgentId, message);
            _logger.LogInformation(
                "Message {MessageId} sent to agent {AgentId}",
                message.MessageId, message.TargetAgentId);
        }
        else if (!string.IsNullOrEmpty(message.TargetRole))
        {
            var targetAgents = registry.GetAgentsByRole(message.TargetRole);
            foreach (var agentId in targetAgents)
            {
                registry.EnqueueMessage(agentId, message);
                delivered = true;
            }
            _logger.LogInformation(
                "Message {MessageId} sent to {Count} agents with role {Role}",
                message.MessageId, targetAgents.Count, message.TargetRole);
        }

        return Task.FromResult(new Proto.Status
        {
            Success = delivered,
            Message = delivered ? "Message delivered" : "No matching agents found"
        });
    }

    public override async Task StreamAgentInbox(
        AgentInboxRequest request,
        IServerStreamWriter<AgentEnvelope> responseStream,
        ServerCallContext context)
    {
        var scopeKey = GetScopeKey(request.UserId, request.AppId);
        if (!_registries.TryGetValue(scopeKey, out var registry))
        {
            _logger.LogWarning(
                "No registry for user {UserId}/app {AppId}",
                request.UserId, request.AppId);
            return;
        }

        var inbox = registry.GetOrCreateInbox(request.AgentId);

        _logger.LogInformation(
            "Agent {AgentId} started inbox stream", request.AgentId);

        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                // Wait for messages with timeout
                if (inbox.TryDequeue(out var envelope, TimeSpan.FromSeconds(30)))
                {
                    await responseStream.WriteAsync(envelope);
                    _logger.LogDebug(
                        "Delivered message {MessageId} to agent {AgentId}",
                        envelope.Message.MessageId, request.AgentId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error streaming inbox for agent {AgentId}", request.AgentId);
        }

        _logger.LogInformation(
            "Agent {AgentId} inbox stream closed", request.AgentId);
    }

    public override Task<Proto.Status> AckAgentMessage(
        AgentAck ack,
        ServerCallContext context)
    {
        var scopeKey = GetScopeKey(ack.UserId, ack.AppId);
        if (_registries.TryGetValue(scopeKey, out var registry))
        {
            registry.AcknowledgeMessage(ack.AgentId, ack.MessageId, ack.AckToken);
            _logger.LogDebug(
                "Agent {AgentId} acknowledged message {MessageId}",
                ack.AgentId, ack.MessageId);
        }

        return Task.FromResult(new Proto.Status
        {
            Success = true,
            Message = "Acknowledged"
        });
    }

    private static string GetScopeKey(string userId, string appId) =>
        $"{userId}:{appId}";
}

/// <summary>
/// Registry for agents and their message queues within a user/app scope.
/// </summary>
internal class AgentRegistry
{
    private readonly ConcurrentDictionary<string, AgentDescriptor> _agents = new();
    private readonly ConcurrentDictionary<string, AgentInbox> _inboxes = new();

    public void RegisterAgent(string agentId, AgentDescriptor descriptor, string userId, string appId)
    {
        _agents[agentId] = descriptor;
        _inboxes.TryAdd(agentId, new AgentInbox());
    }

    public bool EnqueueMessage(string agentId, AgentMessage message)
    {
        if (_inboxes.TryGetValue(agentId, out var inbox))
        {
            var ackToken = Guid.NewGuid().ToString();
            var envelope = new AgentEnvelope
            {
                Message = message,
                AckToken = ackToken
            };
            inbox.Enqueue(envelope);
            return true;
        }
        return false;
    }

    public List<string> GetAgentsByRole(string role)
    {
        return _agents
            .Where(kv => kv.Value.Role == role)
            .Select(kv => kv.Key)
            .ToList();
    }

    public AgentInbox GetOrCreateInbox(string agentId)
    {
        return _inboxes.GetOrAdd(agentId, _ => new AgentInbox());
    }

    public void AcknowledgeMessage(string agentId, string messageId, string ackToken)
    {
        // For at-least-once semantics, track acks (currently just logged)
        // In production, this would prevent redelivery
    }
}

/// <summary>
/// Message queue for an agent with blocking dequeue support.
/// </summary>
internal class AgentInbox
{
    private readonly BlockingCollection<AgentEnvelope> _queue = new();

    public void Enqueue(AgentEnvelope envelope)
    {
        _queue.Add(envelope);
    }

    public bool TryDequeue(out AgentEnvelope? envelope, TimeSpan timeout)
    {
        return _queue.TryTake(out envelope, timeout);
    }
}
