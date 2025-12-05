using Cascade.Grpc;

namespace Cascade.Grpc.Server.Mappers;

internal static class ProtoResults
{
    public static Result Success() => new() { Success = true };

    public static Result Failure(string errorMessage, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode ?? string.Empty
    };
}

