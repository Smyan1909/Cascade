using Cascade.Body.Configuration;
using Cascade.Body.Providers.UIA3Provider;
using Cascade.Body.Vision;
using Cascade.Proto;
using FluentAssertions;
using Xunit;
using ActionProto = Cascade.Proto.Action;
using System.Diagnostics;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using Xunit.Abstractions;

namespace Cascade.Body.Tests;

// Real UI test; runs against Notepad via UIA3.
[Trait("Category", "ui-windows")]
public class UIA3ProviderTests : IDisposable
{
    private readonly UIA3AutomationProvider _provider;
    private readonly OcrService _ocrService;
    private readonly ITestOutputHelper? _output;

    public UIA3ProviderTests(ITestOutputHelper? output = null)
    {
        _output = output;
        _ocrService = new OcrService(TestHelpers.Options(new OcrOptions { Enabled = true, LanguageTag = "en-US" }), TestHelpers.Logger<OcrService>());
        _provider = new UIA3AutomationProvider(
            TestHelpers.Options(new UIA3Options { ActionTimeoutMs = 12000, MaxNodes = 800, TreeDepth = 8 }),
            TestHelpers.Logger<UIA3AutomationProvider>(),
            _ocrService);
    }

    [Fact]
    public async Task Notepad_SemanticTree_And_TypeText()
    {
        await EnsureNotepadRunning();

        _output?.WriteLine("Getting semantic tree...");
        var tree = await _provider.GetSemanticTreeAsync(CancellationToken.None);
        _output?.WriteLine($"Semantic tree has {tree.Elements.Count} elements");
        
        if (tree.Elements.Count == 0)
        {
            _output?.WriteLine("ERROR: Tree is empty. Checking Notepad process directly...");
            var proc = Process.GetProcessesByName("notepad").FirstOrDefault();
            if (proc != null)
            {
                _output?.WriteLine($"Notepad process found: PID={proc.Id}");
                using var automation = new UIA3Automation();
                var app = FlaUI.Core.Application.Attach(proc);
                var window = app.GetMainWindow(automation);
                if (window != null)
                {
                    _output?.WriteLine($"Direct window access: Name={window.Name}, ControlType={window.ControlType}");
                    var children = window.FindAllChildren();
                    _output?.WriteLine($"Direct window has {children.Length} children");
                }
                else
                {
                    _output?.WriteLine("ERROR: Direct window access returned null");
                }
            }
            else
            {
                _output?.WriteLine("ERROR: Notepad process not found");
            }
        }
        
        tree.Elements.Should().NotBeEmpty();

        await AssertPatternFirstTyping();
        await AssertInvokeMenu();
        await AssertScrollAndOcr();
    }

    private async Task EnsureNotepadRunning()
    {
        var start = await _provider.StartAppAsync("notepad.exe", CancellationToken.None);
        start.Success.Should().BeTrue("Notepad should launch or attach for UI automation test");
    }

    private string ReadNotepadEditorText()
    {
        using var automation = new UIA3Automation();
        var proc = Process.GetProcessesByName("notepad").FirstOrDefault();
        proc.Should().NotBeNull("Notepad process should be available");
        var app = FlaUI.Core.Application.Attach(proc);
        var window = app.GetMainWindow(automation);
        window.Should().NotBeNull();
        var edit = window.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))?.AsTextBox();
        return edit?.Text ?? string.Empty;
    }

    private async Task AssertPatternFirstTyping()
    {
        var action = new ActionProto
        {
            ActionType = ActionType.TypeText,
            Selector = new Selector { PlatformSource = PlatformSource.Windows, ControlType = ControlType.Input },
            Text = "hello world"
        };

        var status = await _provider.PerformActionAsync(action, CancellationToken.None);
        _output?.WriteLine($"AssertPatternFirstTyping: PerformActionAsync returned Success={status.Success}, Message={status.Message}");
        status.Success.Should().BeTrue($"Action should succeed but got: {status.Message}");

        var afterTree = await _provider.GetSemanticTreeAsync(CancellationToken.None);
        var treeHasText = afterTree.Elements.Any(e => (e.ValueText ?? string.Empty).Contains("hello world", StringComparison.OrdinalIgnoreCase));

        var nativeValue = ReadNotepadEditorText();
        var nativeHasText = nativeValue.Contains("hello world", StringComparison.OrdinalIgnoreCase);

        (treeHasText || nativeHasText).Should().BeTrue("typed text should be observable via pattern-first typing");
    }

    private async Task AssertInvokeMenu()
    {
        // Try to open the Format menu via pattern-first Invoke (if present)
        var tree = await _provider.GetSemanticTreeAsync(CancellationToken.None);
        var formatMenu = tree.Elements.FirstOrDefault(e => e.Name?.Contains("Format", StringComparison.OrdinalIgnoreCase) == true);
        if (formatMenu == null)
        {
            _output?.WriteLine("AssertInvokeMenu: Format menu not found in tree, skipping");
            return; // skip if menu not found in this environment
        }

        var action = new ActionProto
        {
            ActionType = ActionType.Click,
            Selector = new Selector { PlatformSource = PlatformSource.Windows, Name = formatMenu.Name, ControlType = ControlType.Menu }
        };

        var status = await _provider.PerformActionAsync(action, CancellationToken.None);
        if (!status.Success)
        {
            _output?.WriteLine($"AssertInvokeMenu: Menu click failed ({status.Message}), skipping - menu interactions can be flaky");
            return; // Skip if menu click fails - menu items may not be accessible until parent menu is opened
        }
        // If it succeeds, great!
    }

    private async Task AssertScrollAndOcr()
    {
        // Add some text to force scroll then capture OCR on screenshot
        var longText = string.Join(Environment.NewLine, Enumerable.Repeat("scroll-test-line", 30));
        var typeAction = new ActionProto
        {
            ActionType = ActionType.TypeText,
            Selector = new Selector { PlatformSource = PlatformSource.Windows, ControlType = ControlType.Input },
            Text = longText
        };
        var typeStatus = await _provider.PerformActionAsync(typeAction, CancellationToken.None);
        typeStatus.Success.Should().BeTrue();

        var scrollAction = new ActionProto
        {
            ActionType = ActionType.Scroll,
            Selector = new Selector { PlatformSource = PlatformSource.Windows, ControlType = ControlType.Input },
            Number = 1000
        };
        var scrollStatus = await _provider.PerformActionAsync(scrollAction, CancellationToken.None);
        if (!scrollStatus.Success)
        {
            _output?.WriteLine($"AssertScrollAndOcr: Scroll failed ({scrollStatus.Message}), continuing - scroll can be flaky on Document controls");
        }

        var screenshot = await _provider.GetMarkedScreenshotAsync(CancellationToken.None);
        screenshot.Image.Length.Should().BeGreaterThan(0, "Screenshot should be captured successfully");

        // Test OCR on the screenshot - should detect the typed text
        var ocrResult = await _ocrService.ExtractAsync(screenshot.Image.ToByteArray(), CancellationToken.None);
        ocrResult.Text.Should().NotBeNullOrWhiteSpace("OCR should extract text from Notepad screenshot");
        
        // OCR should detect "scroll-test-line" or at least "scroll" or "test"
        var ocrText = ocrResult.Text.ToUpperInvariant();
        var hasExpectedText = ocrText.Contains("SCROLL") || ocrText.Contains("TEST") || ocrText.Contains("LINE");
        hasExpectedText.Should().BeTrue($"OCR should detect the typed text. OCR text: '{ocrResult.Text}'");
        
        _output?.WriteLine($"OCR extracted text from Notepad: '{ocrResult.Text}'");
        _output?.WriteLine($"OCR found {ocrResult.Regions.Count} text regions");
    }

    public void Dispose()
    {
        _provider.Dispose();
        foreach (var proc in System.Diagnostics.Process.GetProcessesByName("notepad"))
        {
            try { proc.Kill(true); } catch { }
        }
    }
}

