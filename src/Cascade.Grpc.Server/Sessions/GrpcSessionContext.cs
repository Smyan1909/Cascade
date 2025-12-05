namespace Cascade.Grpc.Server.Sessions;

/// <summary>
/// Lightweight ambient context extracted from the protobuf SessionContext message.
/// </summary>
public sealed record GrpcSessionContext(string SessionId, string? AgentId, string? RunId);

