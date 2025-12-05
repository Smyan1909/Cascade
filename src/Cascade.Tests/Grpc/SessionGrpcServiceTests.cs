using Cascade.Database.Enums;
using Cascade.Grpc.Server.Mappers;
using Cascade.Grpc.Server.Services;
using Cascade.Grpc.Server.Sessions;
using Cascade.Grpc.Session;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DbSessionState = Cascade.Database.Enums.SessionState;
using ProtoSessionState = Cascade.Grpc.Session.SessionState;
using AutomationSession = Cascade.Database.Entities.AutomationSession;
using ProtoSessionEvent = Cascade.Grpc.Session.SessionEvent;

namespace Cascade.Tests.Grpc;

public class SessionGrpcServiceTests
{
    private readonly Mock<ISessionLifecycleManager> _lifecycleManager = new();
    private readonly Mock<IUiAutomationSessionManager> _automationManager = new();
    private readonly UiElementRegistry _registry = new();
    private readonly SessionGrpcService _service;

    public SessionGrpcServiceTests()
    {
        _service = new SessionGrpcService(
            _lifecycleManager.Object,
            _automationManager.Object,
            _registry);
    }

    [Fact]
    public async Task CreateSession_Returns_Proto_Response()
    {
        var session = CreateSession();
        _lifecycleManager.Setup(m => m.CreateAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var request = new CreateSessionRequest { AgentId = "agent", RunId = "run" };
        var response = await _service.CreateSession(request, TestServerCallContextFactory.Create("/cascade.session.SessionService/CreateSession"));

        Assert.True(response.Result.Success);
        Assert.Equal(session.SessionId, response.Session.SessionId);
        Assert.Equal(ProtoSessionState.SessionReady, response.State);
    }

    [Fact]
    public async Task ReleaseSession_Throws_When_Id_Missing()
    {
        var request = new ReleaseSessionRequest();

        var call = () => _service.ReleaseSession(request, TestServerCallContextFactory.Create("/cascade.session.SessionService/ReleaseSession"));

        var ex = await Assert.ThrowsAsync<RpcException>(call);
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task StreamEvents_Writes_To_ResponseStream()
    {
        var messages = GetEvents();
        _lifecycleManager.Setup(m => m.SubscribeAsync("agent", It.IsAny<CancellationToken>()))
            .Returns(messages);

        var writer = new TestServerStreamWriter<ProtoSessionEvent>();
        await _service.StreamEvents(
            new SessionEventRequest { AgentId = "agent" },
            writer,
            TestServerCallContextFactory.Create("/cascade.session.SessionService/StreamEvents"));

        Assert.Equal(2, writer.Written.Count);
        Assert.All(writer.Written, evt => Assert.Equal("session-1", evt.Session.SessionId));
    }

    private static AutomationSession CreateSession()
    {
        return new AutomationSession
        {
            SessionId = "session-1",
            AgentId = Guid.NewGuid(),
            RunId = "run",
            Profile = new Cascade.Core.VirtualDesktopProfile { Width = 1920, Height = 1080, Dpi = 100 },
            State = DbSessionState.Active
        };
    }

    private static async IAsyncEnumerable<SessionEventMessage> GetEvents()
    {
        yield return new SessionEventMessage("session-1", "agent", "run", ProtoSessionState.SessionReady, "ready");
        yield return new SessionEventMessage("session-1", "agent", "run", ProtoSessionState.SessionInUse, "in use");
        await Task.CompletedTask;
    }

    private sealed class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public List<T> Written { get; } = new();

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message)
        {
            Written.Add(message);
            return Task.CompletedTask;
        }
    }
}

