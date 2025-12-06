using Cascade.Body.Automation;
using Cascade.Body.Configuration;
using Cascade.Body.Vision;
using Cascade.Proto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using ActionProto = Cascade.Proto.Action;
using StatusProto = Cascade.Proto.Status;

namespace Cascade.Body.Providers.PlaywrightProvider;

public class PlaywrightAutomationProvider : IAutomationProvider, IAsyncDisposable
{
    private readonly PlaywrightOptions _options;
    private readonly BodyOptions _bodyOptions;
    private readonly ILogger<PlaywrightAutomationProvider> _logger;
    private readonly OcrService _ocr;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public PlaywrightAutomationProvider(
        IOptions<PlaywrightOptions> options,
        IOptions<BodyOptions> bodyOptions,
        ILogger<PlaywrightAutomationProvider> logger,
        OcrService ocr)
    {
        _options = options.Value;
        _bodyOptions = bodyOptions.Value;
        _logger = logger;
        _ocr = ocr;
    }

    public PlatformSource Platform => PlatformSource.Web;
    public bool SupportsPatternFirst(Selector selector) => true;

    public async Task<StatusProto> StartAppAsync(string appName, CancellationToken cancellationToken)
    {
        await EnsurePageAsync(cancellationToken).ConfigureAwait(false);

        if (_page is null)
        {
            return new StatusProto { Success = false, Message = "Browser not initialized" };
        }

        try
        {
            await _page.GotoAsync(appName, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _options.ActionTimeoutMs
            }).ConfigureAwait(false);
            return new StatusProto { Success = true, Message = $"Opened {appName}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to {Url}", appName);
            return new StatusProto { Success = false, Message = $"Navigation failed: {ex.Message}" };
        }
    }

    public async Task<SemanticTree> GetSemanticTreeAsync(CancellationToken cancellationToken)
    {
        await EnsurePageAsync(cancellationToken).ConfigureAwait(false);
        if (_page is null)
        {
            return new SemanticTree();
        }

        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
        {
            Timeout = _options.ActionTimeoutMs
        }).ConfigureAwait(false);

        var viewSize = _page.ViewportSize ?? new PageViewportSizeResult { Width = 1280, Height = 720 };
        var locators = _page.Locator("button, input, select, textarea, a, [role]");
        var elements = new List<UIElement>();

        int count = await locators.CountAsync().ConfigureAwait(false);
        count = Math.Min(count, _options.MaxNodes);

        for (int i = 0; i < count; i++)
        {
            var handle = await locators.Nth(i).ElementHandleAsync().ConfigureAwait(false);
            if (handle is null)
            {
                continue;
            }

            var box = await handle.BoundingBoxAsync().ConfigureAwait(false);
            if (box is null)
            {
                continue;
            }

            var id = await handle.GetAttributeAsync("id").ConfigureAwait(false);
            var name = await handle.EvaluateAsync<string>("el => el.getAttribute('name') || el.innerText || el.ariaLabel || ''").ConfigureAwait(false);
            var role = await handle.EvaluateAsync<string>("el => el.getAttribute('role') || ''").ConfigureAwait(false);
            var tag = await handle.EvaluateAsync<string>("el => el.tagName").ConfigureAwait(false);

            var ui = new UIElement
            {
                Id = id ?? Guid.NewGuid().ToString(),
                Name = name ?? string.Empty,
                ControlType = MapControlType(tag),
                BoundingBox = new NormalizedRectangle
                {
                    X = Math.Clamp(box.X / viewSize.Width, 0, 1),
                    Y = Math.Clamp(box.Y / viewSize.Height, 0, 1),
                    Width = Math.Clamp(box.Width / viewSize.Width, 0, 1),
                    Height = Math.Clamp(box.Height / viewSize.Height, 0, 1)
                },
                ParentId = string.Empty,
                PlatformSource = Platform
            };
            if (!string.IsNullOrEmpty(role))
            {
                ui.AriaRole = role;
            }
            if (!string.IsNullOrEmpty(id))
            {
                ui.AutomationId = id;
            }

            if (string.IsNullOrWhiteSpace(ui.Name) && _ocr.IsEnabled)
            {
                try
                {
                    var snap = await handle.ScreenshotAsync(new ElementHandleScreenshotOptions { Type = ScreenshotType.Png }).ConfigureAwait(false);
                    var ocr = await _ocr.ExtractAsync(snap, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(ocr.Text))
                    {
                        ui.Name = ocr.Text;
                        ui.ValueText = ocr.Text;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "OCR failed for DOM element");
                }
            }
            elements.Add(ui);
        }

        return new SemanticTree { Elements = { elements } };
    }

    public async Task<StatusProto> PerformActionAsync(ActionProto action, CancellationToken cancellationToken)
    {
        await EnsurePageAsync(cancellationToken).ConfigureAwait(false);
        if (_page is null)
        {
            return new StatusProto { Success = false, Message = "Browser not initialized" };
        }

        var locator = BuildLocator(_page, action.Selector);
        if (locator is null)
        {
            return new StatusProto { Success = false, Message = "Selector not provided" };
        }

        if (action.Selector?.HasIndex == true && action.Selector.Index >= 0)
        {
            locator = locator.Nth(action.Selector.Index);
        }

        try
        {
            switch (action.ActionType)
            {
                case ActionType.Click:
                    await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    await locator.ClickAsync(new LocatorClickOptions { Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    break;
                case ActionType.TypeText:
                    await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    await locator.FillAsync(action.Text ?? string.Empty, new LocatorFillOptions { Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    break;
                case ActionType.Hover:
                    await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    await locator.HoverAsync(new LocatorHoverOptions { Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    break;
                case ActionType.Focus:
                    await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    await locator.FocusAsync(new LocatorFocusOptions { Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    break;
                case ActionType.Scroll:
                    await locator.EvaluateAsync("el => el.scrollIntoView({behavior:'auto',block:'center'})").ConfigureAwait(false);
                    break;
                case ActionType.WaitVisible:
                    await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = _options.ActionTimeoutMs }).ConfigureAwait(false);
                    break;
                default:
                    return new StatusProto { Success = false, Message = $"Unsupported action {action.ActionType}" };
            }

            return new StatusProto { Success = true, Message = "OK" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playwright action failed");
            return new StatusProto { Success = false, Message = $"Action failed: {ex.Message}" };
        }
    }

    public async Task<Screenshot> GetMarkedScreenshotAsync(CancellationToken cancellationToken)
    {
        await EnsurePageAsync(cancellationToken).ConfigureAwait(false);
        if (_page is null)
        {
            return new Screenshot { Format = ImageFormat.Png };
        }

        var bytes = await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = true
        }).ConfigureAwait(false);

        return new Screenshot
        {
            Image = Google.Protobuf.ByteString.CopyFrom(bytes),
            Format = ImageFormat.Png
        };
    }

    private async Task EnsurePageAsync(CancellationToken cancellationToken)
    {
        if (_page != null)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_page != null)
            {
                return;
            }

            _playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _options.Headless,
                Channel = _options.BrowserChannel
            }).ConfigureAwait(false);
            var context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await context.NewPageAsync().ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(_bodyOptions.DefaultUrl))
            {
                await _page.GotoAsync(_bodyOptions.DefaultUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static ILocator? BuildLocator(IPage page, Selector selector)
    {
        if (selector == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(selector.Id))
        {
            return page.Locator($"#{selector.Id}");
        }

        if (!string.IsNullOrWhiteSpace(selector.Name))
        {
            return page.GetByText(selector.Name);
        }

        if (selector.Path.Count > 0)
        {
            var chained = string.Join(" >> ", selector.Path);
            return page.Locator(chained);
        }

        if (!string.IsNullOrWhiteSpace(selector.TextHint))
        {
            return page.GetByText(selector.TextHint);
        }

        return null;
    }

    private ControlType MapControlType(string tag) =>
        tag.ToUpperInvariant() switch
        {
            "BUTTON" => ControlType.Button,
            "INPUT" => ControlType.Input,
            "SELECT" => ControlType.Combo,
            "TEXTAREA" => ControlType.Input,
            "A" => ControlType.Button,
            _ => ControlType.Custom
        };

    public async ValueTask DisposeAsync()
    {
        if (_page != null)
        {
            await _page.Context.CloseAsync().ConfigureAwait(false);
        }
        if (_browser != null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }
        _playwright?.Dispose();
        _initLock.Dispose();
    }
}

