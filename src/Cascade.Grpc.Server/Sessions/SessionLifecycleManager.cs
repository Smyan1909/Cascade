using Cascade.Core;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Repositories;
using Cascade.Grpc.Session;
using Cascade.Grpc.Server.Mappers;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using DbSessionState = Cascade.Database.Enums.SessionState;
using ProtoSessionState = Cascade.Grpc.Session.SessionState;
using SessionEventEntity = Cascade.Database.Entities.SessionEvent;

namespace Cascade.Grpc.Server.Sessions;

internal sealed class SessionLifecycleManager : ISessionLifecycleManager
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventDispatcher _dispatcher;
    private readonly ILogger<SessionLifecycleManager> _logger;

    public SessionLifecycleManager(
        ISessionRepository sessionRepository,
        ISessionEventDispatcher dispatcher,
        ILogger<SessionLifecycleManager> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger;
    }

    public async Task<AutomationSession> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "CreateSessionRequest is required."));
        }

        if (!Guid.TryParse(request.AgentId, out var agentId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "agent_id must be a valid GUID."));
        }

        var sessionId = Guid.NewGuid();
        var runId = string.IsNullOrWhiteSpace(request.RunId) ? Guid.NewGuid().ToString() : request.RunId;
        var profile = request.Profile.ToDomainProfile();

        var entity = new AutomationSession
        {
            SessionId = sessionId.ToString(),
            AgentId = agentId,
            RunId = runId,
            Profile = profile,
            State = DbSessionState.Active,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _sessionRepository.CreateAsync(entity).ConfigureAwait(false);
        Publish(created, ProtoSessionState.SessionReady, "Session created.");
        return created;
    }

    public async Task<AutomationSession> AttachAsync(AttachSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "session_id is required."));
        }

        var session = await EnsureSessionAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        await _sessionRepository.UpdateStateAsync(session.SessionId, DbSessionState.Active).ConfigureAwait(false);
        session.State = DbSessionState.Active;
        Publish(session, ProtoSessionState.SessionInUse, "Session attached.");
        return session;
    }

    public async Task ReleaseAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        await _sessionRepository.ReleaseAsync(sessionId, reason).ConfigureAwait(false);
        var session = await _sessionRepository.GetBySessionIdAsync(sessionId).ConfigureAwait(false);
        if (session is not null)
        {
            Publish(session, ProtoSessionState.SessionTerminated, reason);
        }
    }

    public async Task<AutomationSession> HeartbeatAsync(SessionHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "session_id is required."));
        }

        var session = await EnsureSessionAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (request.Metrics is not null)
        {
            var payload = $"cpu={request.Metrics.CpuPercent:F2},memory={request.Metrics.MemoryPercent:F2},latency={request.Metrics.InputLatencyMs:F2}";
            await _sessionRepository.AddEventAsync(new SessionEventEntity
            {
                AutomationSessionId = session.Id,
                EventType = "Heartbeat",
                Payload = payload,
                OccurredAt = DateTime.UtcNow
            }).ConfigureAwait(false);
        }

        Publish(session, ProtoSessionState.SessionInUse, "Heartbeat received.");
        return session;
    }

    public IAsyncEnumerable<SessionEventMessage> SubscribeAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "agent_id is required."));
        }

        return _dispatcher.SubscribeAsync(agentId, cancellationToken);
    }

    public async Task<AutomationSession> EnsureSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetBySessionIdAsync(sessionId).ConfigureAwait(false);
        if (session is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Session '{sessionId}' was not found."));
        }

        return session;
    }

    public async Task UpdateStateAsync(string sessionId, ProtoSessionState state, CancellationToken cancellationToken = default)
    {
        var dbState = state switch
        {
            ProtoSessionState.SessionDraining => DbSessionState.Draining,
            ProtoSessionState.SessionTerminated => DbSessionState.Released,
            _ => DbSessionState.Active
        };

        await _sessionRepository.UpdateStateAsync(sessionId, dbState).ConfigureAwait(false);
    }

    private void Publish(AutomationSession session, ProtoSessionState state, string message)
    {
        _dispatcher.Publish(new SessionEventMessage(
            session.SessionId,
            session.AgentId.ToString(),
            session.RunId,
            state,
            message));
    }
}

