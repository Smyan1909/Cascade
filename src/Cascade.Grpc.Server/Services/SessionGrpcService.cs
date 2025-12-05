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

    public override async Task<SessionResponse> CreateSession(CreateSessionRequest request, ServerCallContext context)
    {
        var session = await _lifecycleManager.CreateAsync(request, context.CancellationToken).ConfigureAwait(false);
        return session.ToSessionResponse();
    }

    public override async Task<SessionResponse> AttachSession(AttachSessionRequest request, ServerCallContext context)
    {
        var session = await _lifecycleManager.AttachAsync(request, context.CancellationToken).ConfigureAwait(false);
        return session.ToSessionResponse();
    }

    public override async Task<Result> ReleaseSession(ReleaseSessionRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "session_id is required."));
        }

        await _lifecycleManager.ReleaseAsync(request.SessionId, request.Reason ?? "Released via API", context.CancellationToken).ConfigureAwait(false);
        _automationSessionManager.Invalidate(request.SessionId);
        _elementRegistry.InvalidateSession(request.SessionId);
        return ProtoResults.Success();
    }

    public override async Task<SessionResponse> Heartbeat(SessionHeartbeatRequest request, ServerCallContext context)
    {
        var session = await _lifecycleManager.HeartbeatAsync(request, context.CancellationToken).ConfigureAwait(false);
        return session.ToSessionResponse();
    }

    public override async Task StreamEvents(SessionEventRequest request, IServerStreamWriter<SessionEvent> responseStream, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "agent_id is required."));
        }

        await foreach (var evt in _lifecycleManager.SubscribeAsync(request.AgentId, context.CancellationToken).ConfigureAwait(false))
        {
            await responseStream.WriteAsync(evt.ToProtoEvent()).ConfigureAwait(false);
        }
    }
}

