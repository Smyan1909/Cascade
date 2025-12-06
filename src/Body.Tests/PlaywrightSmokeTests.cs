using Cascade.Body.Configuration;
using Cascade.Body.Providers.PlaywrightProvider;
using Cascade.Body.Vision;
using Cascade.Proto;
using FluentAssertions;
using Xunit;
using ActionProto = Cascade.Proto.Action;

namespace Cascade.Body.Tests;

[Trait("Category", "playwright-smoke")]
public class PlaywrightSmokeTests : IAsyncLifetime
{
    private PlaywrightAutomationProvider _provider = null!;

    private OcrService _ocrService = null!;

    public async Task InitializeAsync()
    {
        _ocrService = new OcrService(TestHelpers.Options(new OcrOptions { Enabled = true, LanguageTag = "en-US" }), TestHelpers.Logger<OcrService>());
        _provider = new PlaywrightAutomationProvider(
            TestHelpers.Options(new PlaywrightOptions { Headless = true, ActionTimeoutMs = 10000 }),
            TestHelpers.Options(new BodyOptions { DefaultUrl = string.Empty }),
            TestHelpers.Logger<PlaywrightAutomationProvider>(),
            _ocrService);

        var html = "<html><body><div id='scroll' style='height:1500px;'><input id='name'/><button id='go'>Go</button><h1>Test Page</h1></div></body></html>";
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".html");
        await File.WriteAllTextAsync(tmp, html);
        var uri = new Uri(tmp).AbsoluteUri;
        var start = await _provider.StartAppAsync(uri, CancellationToken.None);
        start.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CanTypeClickScrollAndGetTree()
    {
        var tree = await _provider.GetSemanticTreeAsync(CancellationToken.None);
        tree.Elements.Should().Contain(e => e.Id == "name");

        var typeStatus = await _provider.PerformActionAsync(new ActionProto
        {
            ActionType = ActionType.TypeText,
            Selector = new Selector { Id = "name", PlatformSource = PlatformSource.Web },
            Text = "abc"
        }, CancellationToken.None);
        typeStatus.Success.Should().BeTrue();

        var clickStatus = await _provider.PerformActionAsync(new ActionProto
        {
            ActionType = ActionType.Click,
            Selector = new Selector { Id = "go", PlatformSource = PlatformSource.Web }
        }, CancellationToken.None);
        clickStatus.Success.Should().BeTrue();

        var scrollStatus = await _provider.PerformActionAsync(new ActionProto
        {
            ActionType = ActionType.Scroll,
            Selector = new Selector { Id = "scroll", PlatformSource = PlatformSource.Web }
        }, CancellationToken.None);
        scrollStatus.Success.Should().BeTrue();

        var screenshot = await _provider.GetMarkedScreenshotAsync(CancellationToken.None);
        screenshot.Image.Length.Should().BeGreaterThan(0, "Screenshot should be captured");

        // Test OCR on the screenshot - should detect text on the page
        var ocrResult = await _ocrService.ExtractAsync(screenshot.Image.ToByteArray(), CancellationToken.None);
        ocrResult.Text.Should().NotBeNullOrWhiteSpace("OCR should extract text from web page screenshot");
        
        // OCR should detect some text from the page (like "Go", "Test Page", or typed "abc")
        var ocrText = ocrResult.Text.ToUpperInvariant();
        var hasExpectedText = ocrText.Contains("GO") || ocrText.Contains("TEST") || ocrText.Contains("ABC");
        hasExpectedText.Should().BeTrue($"OCR should detect text from the page. OCR text: '{ocrResult.Text}'");
        
        System.Console.WriteLine($"OCR extracted text from web page: '{ocrResult.Text}'");
        System.Console.WriteLine($"OCR found {ocrResult.Regions.Count} text regions");
    }

    public async Task DisposeAsync()
    {
        if (_provider != null)
        {
            await _provider.DisposeAsync();
        }
    }
}

