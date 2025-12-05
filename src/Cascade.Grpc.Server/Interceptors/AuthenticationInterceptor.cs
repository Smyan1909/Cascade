using Cascade.Grpc.Server.Startup;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cascade.Grpc.Server.Interceptors;

public sealed class AuthenticationInterceptor : Interceptor
{
    private readonly IOptionsMonitor<GrpcServerOptions> _options;
    private readonly ILogger<AuthenticationInterceptor> _logger;

    public AuthenticationInterceptor(
        IOptionsMonitor<GrpcServerOptions> options,
        ILogger<AuthenticationInterceptor> logger)
    {
        _options = options;
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthenticated(context);
        return await continuation(request, context).ConfigureAwait(false);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthenticated(context);
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthenticated(context);
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthenticated(context);
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private void EnsureAuthenticated(ServerCallContext context)
    {
        var options = _options.CurrentValue;
        if (!options.RequireAuthentication || options.ApiKeys is null || options.ApiKeys.Count == 0)
        {
            return;
        }

        if (TryAuthenticate(context.RequestHeaders, options.ApiKeys))
        {
            return;
        }

        _logger.LogWarning("Unauthenticated gRPC call for method {Method}.", context.Method);
        throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid authentication token."));
    }

    private static bool TryAuthenticate(Metadata headers, IEnumerable<string> validApiKeys)
    {
        var apiKeys = validApiKeys.ToHashSet(StringComparer.Ordinal);

        var header = headers.FirstOrDefault(h => string.Equals(h.Key, "x-api-key", StringComparison.OrdinalIgnoreCase));
        if (header is not null && apiKeys.Contains(header.Value))
        {
            return true;
        }

        var authHeader = headers.FirstOrDefault(h => string.Equals(h.Key, "authorization", StringComparison.OrdinalIgnoreCase));
        if (authHeader is null)
        {
            return false;
        }

        if (authHeader.Value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Value.Substring("Bearer ".Length).Trim();
            return apiKeys.Contains(token);
        }

        return apiKeys.Contains(authHeader.Value);
    }
}

