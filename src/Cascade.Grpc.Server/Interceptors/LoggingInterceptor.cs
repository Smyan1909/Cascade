using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Cascade.Grpc.Server.Interceptors;

public sealed class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        return await LogCallAsync(context.Method, () => continuation(request, context)).ConfigureAwait(false);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        return await LogCallAsync(context.Method, () => continuation(requestStream, context)).ConfigureAwait(false);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await LogCallAsync(context.Method, async () =>
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
        await LogCallAsync(context.Method, async () =>
        {
            await continuation(requestStream, responseStream, context).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    private async Task<T> LogCallAsync<T>(string method, Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Handling gRPC call {Method}", method);
            var response = await action().ConfigureAwait(false);
            _logger.LogDebug("Completed gRPC call {Method} in {Elapsed} ms", method, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call {Method} failed with status {Status}", method, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC call {Method} failed unexpectedly", method);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}

