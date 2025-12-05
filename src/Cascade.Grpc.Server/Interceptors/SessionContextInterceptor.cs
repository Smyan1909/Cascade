using Cascade.Grpc.Server.Sessions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Cascade.Grpc.Server.Interceptors;

public sealed class SessionContextInterceptor : Interceptor
{
    private readonly IGrpcSessionContextAccessor _sessionAccessor;
    private readonly ILogger<SessionContextInterceptor> _logger;

    public SessionContextInterceptor(
        IGrpcSessionContextAccessor sessionAccessor,
        ILogger<SessionContextInterceptor> logger)
    {
        _sessionAccessor = sessionAccessor;
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        return await WithSessionAsync(request, () => continuation(request, context)).ConfigureAwait(false);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await WithSessionAsync(request, async () =>
        {
            await continuation(request, responseStream, context).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private async Task<T> WithSessionAsync<TRequest, T>(TRequest request, Func<Task<T>> action)
    {
        var session = ExtractSession(request);
        if (session is not null)
        {
            _sessionAccessor.Current = session;
        }

        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            if (session is not null)
            {
                _sessionAccessor.Current = null;
            }
        }
    }

    private static GrpcSessionContext? ExtractSession<TRequest>(TRequest request)
    {
        if (request is not IMessage message)
        {
            return null;
        }

        var field = message.Descriptor.Fields.InFieldNumberOrder().FirstOrDefault(IsSessionField);
        if (field is null)
        {
            return null;
        }

        if (field.Accessor.GetValue(message) is not IMessage value)
        {
            return null;
        }

        var sessionId = GetStringField(value, "session_id");
        var agentId = GetStringField(value, "agent_id");
        var runId = GetStringField(value, "run_id");

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        return new GrpcSessionContext(sessionId, agentId, runId);
    }

    private void Validate(string method, GrpcSessionContext? session)
    {
        // Validation disabled in current-session mode.
        if (session is null)
        {
            _logger.LogDebug("SessionContext missing for method {Method}; proceeding with current-session defaults.", method);
        }
    }

    private static bool IsSessionField(FieldDescriptor descriptor)
    {
        return descriptor.Name.Equals("session", StringComparison.OrdinalIgnoreCase) &&
               descriptor.FieldType == FieldType.Message;
    }

    private static string? GetStringField(IMessage message, string fieldName)
    {
        var field = message.Descriptor.FindFieldByName(fieldName);
        if (field is null)
        {
            return null;
        }

        var value = field.Accessor.GetValue(message);
        return value?.ToString();
    }

}

