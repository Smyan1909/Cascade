using Cascade.Body.Configuration;
using Cascade.Proto;
using Microsoft.Extensions.Options;

namespace Cascade.Body.Automation;

public class AutomationRouter
{
    private readonly IReadOnlyDictionary<PlatformSource, IAutomationProvider> _providers;
    private readonly BodyOptions _options;

    public AutomationRouter(IEnumerable<IAutomationProvider> providers, IOptions<BodyOptions> options)
    {
        _providers = providers.ToDictionary(p => p.Platform, p => p);
        _options = options.Value;
    }

    public IAutomationProvider? GetProvider(PlatformSource? platform)
    {
        var target = platform == null || platform == PlatformSource.Unspecified
            ? _options.DefaultPlatform
            : platform.Value;

        return _providers.TryGetValue(target, out var provider) ? provider : null;
    }

    public IEnumerable<IAutomationProvider> AllProviders => _providers.Values;
}

