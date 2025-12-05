using Cascade.Grpc.Session;

namespace Cascade.Grpc.Server.Sessions;

public sealed record SessionEventMessage(
    string SessionId,
    string? AgentId,
    string? RunId,
    SessionState State,
    string Message);

