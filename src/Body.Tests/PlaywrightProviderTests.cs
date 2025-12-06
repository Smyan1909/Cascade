using Cascade.Body.Configuration;
using Cascade.Body.Providers.PlaywrightProvider;
using Cascade.Body.Vision;
using Cascade.Proto;
using FluentAssertions;
using Xunit;
using ActionProto = Cascade.Proto.Action;

namespace Cascade.Body.Tests;

public class PlaywrightProviderTests
{
    [Fact]
    public async Task StartAndInteractWithLocalPage()
    {
        var html = "<html><body><input id='name' value='' /><button id='go'>Go</button></body></html>";
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".html");
        await File.WriteAllTextAsync(tmp, html);
        var uri = new Uri(tmp).AbsoluteUri;

        await using var provider = new PlaywrightAutomationProvider(
            TestHelpers.Options(new PlaywrightOptions { Headless = true, ActionTimeoutMs = 10000 }),
            TestHelpers.Options(new BodyOptions { DefaultUrl = uri }),
            TestHelpers.Logger<PlaywrightAutomationProvider>(),
            new OcrService(TestHelpers.Options(new OcrOptions { Enabled = false }), TestHelpers.Logger<OcrService>()));

        var start = await provider.StartAppAsync(uri, CancellationToken.None);
        start.Success.Should().BeTrue();

        var tree = await provider.GetSemanticTreeAsync(CancellationToken.None);
        tree.Elements.Should().Contain(e => e.Id == "name");

        var typeStatus = await provider.PerformActionAsync(new ActionProto
        {
            ActionType = ActionType.TypeText,
            Selector = new Selector { Id = "name", PlatformSource = PlatformSource.Web },
            Text = "abc"
        }, CancellationToken.None);
        typeStatus.Success.Should().BeTrue();
    }
}

