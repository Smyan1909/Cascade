using Cascade.Body.Automation;
using Cascade.Body.Configuration;
using Cascade.Proto;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using ActionProto = Cascade.Proto.Action;
using StatusProto = Cascade.Proto.Status;

namespace Cascade.Body.Tests;

public class AutomationRouterTests
{
    private class FakeProvider : IAutomationProvider
    {
        public FakeProvider(PlatformSource platform) => Platform = platform;
        public PlatformSource Platform { get; }
        public Task<StatusProto> StartAppAsync(string appName, CancellationToken cancellationToken) => Task.FromResult(new StatusProto { Success = true });
        public Task<SemanticTree> GetSemanticTreeAsync(CancellationToken cancellationToken) => Task.FromResult(new SemanticTree());
        public Task<StatusProto> PerformActionAsync(ActionProto action, CancellationToken cancellationToken) => Task.FromResult(new StatusProto { Success = true });
        public Task<Screenshot> GetMarkedScreenshotAsync(CancellationToken cancellationToken) => Task.FromResult(new Screenshot());
        public bool SupportsPatternFirst(Selector selector) => true;
    }

    [Fact]
    public void ReturnsProviderForPlatformOrDefault()
    {
        var providers = new IAutomationProvider[]
        {
            new FakeProvider(PlatformSource.Windows),
            new FakeProvider(PlatformSource.Web)
        };
        var opts = Options.Create(new BodyOptions { DefaultPlatform = PlatformSource.Web });
        var router = new AutomationRouter(providers, opts);

        router.GetProvider(PlatformSource.Windows).Should().NotBeNull();
        router.GetProvider(PlatformSource.Unspecified)!.Platform.Should().Be(PlatformSource.Web);
    }
}

