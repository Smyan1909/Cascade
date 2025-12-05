using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Grpc.Session;
using ProtoSessionState = Cascade.Grpc.Session.SessionState;

namespace Cascade.Grpc.Server.Sessions;

public interface ISessionLifecycleManager
{
    Task<AutomationSession> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default);
    Task<AutomationSession> AttachAsync(AttachSessionRequest request, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string sessionId, string reason, CancellationToken cancellationToken = default);
    Task<AutomationSession> HeartbeatAsync(SessionHeartbeatRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<SessionEventMessage> SubscribeAsync(string agentId, CancellationToken cancellationToken = default);
    Task<AutomationSession> EnsureSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task UpdateStateAsync(string sessionId, ProtoSessionState state, CancellationToken cancellationToken = default);
}

