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
    public async Task UnaryServerHandler_Allows_Missing_Session()
    {
        var context = TestServerCallContextFactory.Create("/cascade.uiautomation.UIAutomationService/GetDesktopRoot");

        var response = await _interceptor.UnaryServerHandler(
            new Empty(),
            context,
            (request, _) => Task.FromResult(new ElementResponse()));

        Assert.NotNull(response);
        Assert.Null(_accessor.Current);
    }

    [Fact]
    public async Task UnaryServerHandler_NoSessionField_LeavesContextNull()
    {
        var request = new FindElementRequest();

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
        Assert.Null(captured);
        Assert.Null(_accessor.Current);
    }
}

