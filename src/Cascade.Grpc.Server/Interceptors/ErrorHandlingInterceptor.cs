using Cascade.UIAutomation.Services;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Cascade.Grpc.Server.Interceptors;

public sealed class ErrorHandlingInterceptor : Interceptor
{
    private readonly ILogger<ErrorHandlingInterceptor> _logger;

    public ErrorHandlingInterceptor(ILogger<ErrorHandlingInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        return await HandleAsync(() => continuation(request, context)).ConfigureAwait(false);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        return await HandleAsync(() => continuation(requestStream, context)).ConfigureAwait(false);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await HandleAsync(async () =>
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
        await HandleAsync(async () =>
        {
            await continuation(requestStream, responseStream, context).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    private async Task<T> HandleAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (UIAutomationException ex)
        {
            _logger.LogWarning(ex, "UIAutomation error: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Operation timed out.");
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, ex.Message));
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Operation cancelled.");
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled server error.");
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }
}

