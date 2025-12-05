using System.Net;
using Cascade.Core;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Grpc.Server.Interceptors;
using Cascade.Grpc.Server.Services;
using Cascade.Grpc.Server.Sessions;
using Cascade.Grpc.Server.Startup;
using Cascade.Grpc.Session;
using Cascade.UIAutomation.Services;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using Xunit;
using DbSessionState = Cascade.Database.Enums.SessionState;
using ProtoSessionState = Cascade.Grpc.Session.SessionState;
using CoreVirtualDesktopProfile = Cascade.Core.VirtualDesktopProfile;

namespace Cascade.Tests.Grpc;

public class GrpcServerSmokeTests
{
    [Fact]
    public async Task SessionService_CreateSession_Runs_EndToEnd()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging(logging => logging.AddProvider(NullLoggerProvider.Instance));

        builder.Services.Configure<GrpcServerOptions>(options =>
        {
            options.RequireAuthentication = false;
            options.Port = 50051;
        });

        builder.Services.AddSingleton<IGrpcSessionContextAccessor, GrpcSessionContextAccessor>();
        builder.Services.AddSingleton<ISessionLifecycleManager, FakeSessionLifecycleManager>();
        builder.Services.AddSingleton<IUiAutomationSessionManager, FakeUiAutomationSessionManager>();
        builder.Services.AddSingleton<UiElementRegistry>();
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<ErrorHandlingInterceptor>();
            options.Interceptors.Add<LoggingInterceptor>();
            options.Interceptors.Add<AuthenticationInterceptor>();
            options.Interceptors.Add<SessionContextInterceptor>();
        });

        var app = builder.Build();
        app.MapGrpcService<SessionGrpcService>();

        await app.StartAsync();

        var httpClient = app.GetTestClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        httpClient.DefaultRequestVersion = HttpVersion.Version20;
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

        using var channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = httpClient
        });

        var client = new SessionService.SessionServiceClient(channel);
        var response = await client.CreateSessionAsync(new CreateSessionRequest { AgentId = "agent", RunId = "run" });

        Assert.True(response.Result.Success);
        Assert.Equal("session-hosted", response.Session.SessionId);

        await app.StopAsync();
    }

    private sealed class FakeSessionLifecycleManager : ISessionLifecycleManager
    {
        public Task<AutomationSession> AttachAsync(AttachSessionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateSession());

        public Task<AutomationSession> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateSession());

        public Task<AutomationSession> EnsureSessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateSession());

        public Task<AutomationSession> HeartbeatAsync(SessionHeartbeatRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateSession());

        public Task ReleaseAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public IAsyncEnumerable<SessionEventMessage> SubscribeAsync(string agentId, CancellationToken cancellationToken = default)
            => Empty();

        public Task UpdateStateAsync(string sessionId, ProtoSessionState state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private static AutomationSession CreateSession()
        {
            return new AutomationSession
            {
                SessionId = "session-hosted",
                AgentId = Guid.NewGuid(),
                RunId = "run",
                Profile = CoreVirtualDesktopProfile.Default,
                State = DbSessionState.Active
            };
        }

        private static async IAsyncEnumerable<SessionEventMessage> Empty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeUiAutomationSessionManager : IUiAutomationSessionManager
    {
        public void Invalidate(string sessionId)
        {
        }

        public Task<IUIAutomationService> GetServiceAsync(GrpcSessionContext session, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("UI Automation not required for this smoke test.");
    }
}

