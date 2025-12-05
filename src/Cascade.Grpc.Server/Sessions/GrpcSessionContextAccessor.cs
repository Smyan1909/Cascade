using System.Threading;

namespace Cascade.Grpc.Server.Sessions;

public interface IGrpcSessionContextAccessor
{
    GrpcSessionContext? Current { get; set; }
}

public sealed class GrpcSessionContextAccessor : IGrpcSessionContextAccessor
{
    private readonly AsyncLocal<GrpcSessionContext?> _current = new();

    public GrpcSessionContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

