using Cascade.Grpc.Server.Interceptors;
using Cascade.Grpc.Server.Startup;
using Cascade.Grpc.UIAutomation;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using ProtoResult = Cascade.Grpc.Result;

namespace Cascade.Tests.Grpc;

public class AuthenticationInterceptorTests
{
    [Fact]
    public async Task Throws_When_ApiKey_Missing()
    {
        var options = CreateOptions(new GrpcServerOptions
        {
            RequireAuthentication = true,
            ApiKeys = new List<string> { "secret" }
        });
        var interceptor = new AuthenticationInterceptor(options, NullLogger<AuthenticationInterceptor>.Instance);
        var context = TestServerCallContextFactory.Create("/cascade.session.SessionService/CreateSession");

        var call = () => interceptor.UnaryServerHandler(
            new Empty(),
            context,
            (request, _) => Task.FromResult(new ElementResponse()));

        var ex = await Assert.ThrowsAsync<RpcException>(call);
        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [Fact]
    public async Task Succeeds_When_ApiKey_Present()
    {
        var options = CreateOptions(new GrpcServerOptions
        {
            RequireAuthentication = true,
            ApiKeys = new List<string> { "secret" }
        });

        var interceptor = new AuthenticationInterceptor(options, NullLogger<AuthenticationInterceptor>.Instance);
        var headers = new Metadata { { "x-api-key", "secret" } };
        var context = TestServerCallContextFactory.Create("/cascade.session.SessionService/CreateSession", headers);

        var response = await interceptor.UnaryServerHandler(
            new Empty(),
            context,
            (request, _) => Task.FromResult(new ElementResponse { Result = new ProtoResult { Success = true } }));

        Assert.True(response.Result.Success);
    }

    private static IOptionsMonitor<GrpcServerOptions> CreateOptions(GrpcServerOptions options)
    {
        return new StaticOptionsMonitor<GrpcServerOptions>(options);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

