using Cascade.Body.Automation;
using Cascade.Body.Configuration;
using Cascade.Proto;
using Grpc.Core;
using Microsoft.Extensions.Options;
using StatusProto = Cascade.Proto.Status;

namespace Cascade.Body.Services;

public class SessionService : Proto.SessionService.SessionServiceBase
{
    private readonly AutomationRouter _router;
    private readonly BodyOptions _options;

    public SessionService(AutomationRouter router, IOptions<BodyOptions> options)
    {
        _router = router;
        _options = options.Value;
    }

    public override async Task<StatusProto> StartApp(StartAppRequest request, ServerCallContext context)
    {
        var appName = request.AppName?.Trim();
        if (string.IsNullOrWhiteSpace(appName))
        {
            return new StatusProto { Success = false, Message = "app_name is required" };
        }

        var platform = LooksLikeUrl(appName)
            ? PlatformSource.Web
            : _options.DefaultPlatform;

        var provider = _router.GetProvider(platform);
        if (provider is null)
        {
            return new StatusProto { Success = false, Message = $"No provider registered for platform {platform}" };
        }

        return await provider.StartAppAsync(appName, context.CancellationToken).ConfigureAwait(false);
    }

    public override Task<StatusProto> ResetState(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
    {
        // A lightweight reset can be implemented per-provider in the future.
        return Task.FromResult(new StatusProto { Success = true, Message = "ResetState acknowledged" });
    }

    private static bool LooksLikeUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

