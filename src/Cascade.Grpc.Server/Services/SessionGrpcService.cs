using Cascade.Grpc.Server.Mappers;
using Cascade.Grpc.Server.Sessions;
using Cascade.Grpc.Session;
using Grpc.Core;

namespace Cascade.Grpc.Server.Services;

public sealed class SessionGrpcService : SessionService.SessionServiceBase
{
    private readonly ISessionLifecycleManager _lifecycleManager;
    private readonly IUiAutomationSessionManager _automationSessionManager;
    private readonly UiElementRegistry _elementRegistry;

    public SessionGrpcService(
        ISessionLifecycleManager lifecycleManager,
        IUiAutomationSessionManager automationSessionManager,
        UiElementRegistry elementRegistry)
    {
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _automationSessionManager = automationSessionManager ?? throw new ArgumentNullException(nameof(automationSessionManager));
        _elementRegistry = elementRegistry ?? throw new ArgumentNullException(nameof(elementRegistry));
    }

    public override Task<SessionResponse> CreateSession(CreateSessionRequest request, ServerCallContext context)
    {
        // Current-session mode: return a stub session context.
        var sessionId = Guid.NewGuid().ToString();
        var response = new SessionResponse
        {
            Result = ProtoResults.Success(),
            Session = new SessionContext
            {
                SessionId = sessionId,
                AgentId = string.IsNullOrWhiteSpace(request.AgentId) ? "local-agent" : request.AgentId,
                RunId = string.IsNullOrWhiteSpace(request.RunId) ? Guid.NewGuid().ToString() : request.RunId
            }
        };
        return Task.FromResult(response);
    }

    public override Task<Result> ReleaseSession(ReleaseSessionRequest request, ServerCallContext context)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? "local" : request.SessionId;
        _automationSessionManager.Invalidate(sessionId);
        _elementRegistry.InvalidateSession(sessionId);
        return Task.FromResult(ProtoResults.Success());
    }

    public override Task<SessionResponse> Heartbeat(SessionHeartbeatRequest request, ServerCallContext context)
    {
        var response = new SessionResponse
        {
            Result = ProtoResults.Success(),
            Session = new SessionContext
            {
                SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? "local" : request.SessionId,
                AgentId = "local-agent",
                RunId = Guid.NewGuid().ToString()
            }
        };
        return Task.FromResult(response);
    }

    public override async Task StreamEvents(SessionEventRequest request, IServerStreamWriter<SessionEvent> responseStream, ServerCallContext context)
    {
        // No-op stream in current-session mode.
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

