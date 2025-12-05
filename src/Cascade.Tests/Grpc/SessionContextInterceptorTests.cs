using Cascade.Grpc.Server.Interceptors;
using Cascade.Grpc.Server.Sessions;
using Cascade.Grpc.UIAutomation;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProtoSessionContext = Cascade.Grpc.SessionContext;
using ProtoResult = Cascade.Grpc.Result;

namespace Cascade.Tests.Grpc;

public class SessionContextInterceptorTests
{
    private readonly GrpcSessionContextAccessor _accessor = new();
    private readonly SessionContextInterceptor _interceptor;

    public SessionContextInterceptorTests()
    {
        _interceptor = new SessionContextInterceptor(_accessor, NullLogger<SessionContextInterceptor>.Instance);
    }

    [Fact]
    public async Task UnaryServerHandler_Throws_WhenSessionMissing()
    {
        var context = TestServerCallContextFactory.Create("/cascade.uiautomation.UIAutomationService/GetDesktopRoot");

        var call = () => _interceptor.UnaryServerHandler(
            new Empty(),
            context,
            (request, _) => Task.FromResult(new ElementResponse()));

        var exception = await Assert.ThrowsAsync<RpcException>(call);
        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Null(_accessor.Current);
    }

    [Fact]
    public async Task UnaryServerHandler_PopulatesContext_WhenSessionPresent()
    {
        var request = new FindElementRequest
        {
            Session = new ProtoSessionContext
            {
                SessionId = "session-123",
                AgentId = "agent",
                RunId = "run"
            }
        };

        GrpcSessionContext? captured = null;
        var context = TestServerCallContextFactory.Create("/cascade.uiautomation.UIAutomationService/FindElement");

        var response = await _interceptor.UnaryServerHandler(
            request,
            context,
            (req, _) =>
            {
                captured = _accessor.Current;
                return Task.FromResult(new ElementResponse { Result = new ProtoResult { Success = true } });
            });

        Assert.True(response.Result.Success);
        Assert.NotNull(captured);
        Assert.Equal("session-123", captured!.SessionId);
        Assert.Null(_accessor.Current);
    }
}

