namespace Cascade.Grpc.Server.Sessions;

public interface ISessionRuntimeResolver
{
    Task<SessionRuntime> ResolveAsync(GrpcSessionContext context, CancellationToken cancellationToken = default);
}

