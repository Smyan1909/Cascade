using Cascade.Core;
using Cascade.Core.Session;
using Cascade.Database.Repositories;
using Cascade.UIAutomation.Session;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Windows.Automation;

namespace Cascade.Grpc.Server.Sessions;

internal sealed class SessionRuntimeResolver : ISessionRuntimeResolver
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SessionRuntimeResolver> _logger;

    public SessionRuntimeResolver(
        ISessionRepository sessionRepository,
        ILogger<SessionRuntimeResolver> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger;
    }

    public async Task<SessionRuntime> ResolveAsync(GrpcSessionContext context, CancellationToken cancellationToken = default)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.SessionId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "session_id is required."));
        }

        var session = await _sessionRepository.GetBySessionIdAsync(context.SessionId).ConfigureAwait(false);
        if (session is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Session '{context.SessionId}' was not found."));
        }

        var handle = BuildHandle(session.SessionId, session.RunId, session.Profile);
        var input = BuildInputChannel(session.SessionId);
        var rootElement = AutomationElement.RootElement;

        if (rootElement is null)
        {
            _logger.LogError("Automation root element is not available.");
            throw new RpcException(new Status(StatusCode.Internal, "UI Automation root element is not available."));
        }

        return new SessionRuntime(handle, input, rootElement);
    }

    private static SessionHandle BuildHandle(string sessionId, string runId, VirtualDesktopProfile profile)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid))
        {
            sessionGuid = Guid.NewGuid();
        }

        if (!Guid.TryParse(runId, out var runGuid))
        {
            runGuid = Guid.NewGuid();
        }

        return new SessionHandle
        {
            SessionId = sessionGuid,
            RunId = runGuid,
            VirtualDesktopId = IntPtr.Zero,
            UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            DesktopProfile = profile ?? VirtualDesktopProfile.Default,
            AcquiredAt = DateTimeOffset.UtcNow
        };
    }

    private static VirtualInputChannel BuildInputChannel(string sessionId)
    {
        return new VirtualInputChannel
        {
            ChannelId = Guid.NewGuid(),
            DevicePath = $@"\\.\cascade\input\{sessionId}",
            Transport = "virtual",
            Profile = VirtualInputProfile.Balanced,
            LatencyBudget = TimeSpan.FromMilliseconds(35)
        };
    }
}

