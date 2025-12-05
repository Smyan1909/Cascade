using System.Diagnostics;
using System.Linq;
using Cascade.Grpc.Server.Mappers;
using Cascade.Grpc.Server.Sessions;
using Cascade.Grpc.UIAutomation;
using Cascade.UIAutomation.Discovery;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Input;
using Cascade.UIAutomation.Patterns;
using Cascade.UIAutomation.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UIAutomationProtoService = Cascade.Grpc.UIAutomation.UIAutomationService;
using UiSearchCriteria = Cascade.UIAutomation.Discovery.SearchCriteria;
using DrawingPoint = System.Drawing.Point;

namespace Cascade.Grpc.Server.Services;

public sealed class UIAutomationGrpcService : UIAutomationProtoService.UIAutomationServiceBase
{
    private readonly IUiAutomationSessionManager _sessionManager;
    private readonly IGrpcSessionContextAccessor _sessionAccessor;
    private readonly UiElementRegistry _elementRegistry;
    private readonly ILogger<UIAutomationGrpcService> _logger;

    public UIAutomationGrpcService(
        IUiAutomationSessionManager sessionManager,
        IGrpcSessionContextAccessor sessionAccessor,
        UiElementRegistry elementRegistry,
        ILogger<UIAutomationGrpcService> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));
        _elementRegistry = elementRegistry ?? throw new ArgumentNullException(nameof(elementRegistry));
        _logger = logger;
    }

    public override async Task<ElementResponse> GetDesktopRoot(Empty request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = automation.Discovery.GetDesktopRoot();
        return BuildElementResponse(session.SessionId, element);
    }

    public override async Task<ElementResponse> GetForegroundWindow(Empty request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = automation.Discovery.GetForegroundWindow();
        if (element is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "No foreground window detected."));
        }

        return BuildElementResponse(session.SessionId, element);
    }

    public override async Task<ElementResponse> FindWindow(FindWindowRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "title is required."));
        }

        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        IUIElement? match;
        if (request.ExactMatch)
        {
            match = automation.Discovery.FindWindow(request.Title);
        }
        else
        {
            match = automation
                .Discovery
                .GetAllWindows()
                .FirstOrDefault(window => window.Name?.Contains(request.Title, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (match is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Window '{request.Title}' was not found."));
        }

        return BuildElementResponse(session.SessionId, match);
    }

    public override async Task<ElementResponse> FindElement(FindElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var criteria = request.Criteria?.ToDomainCriteria() ?? new UiSearchCriteria();

        IUIElement? root = null;
        if (!string.IsNullOrWhiteSpace(request.RuntimeId))
        {
            root = ResolveElement(session.SessionId, request.RuntimeId);
        }

        IUIElement? result;
        if (root is not null)
        {
            result = root.FindFirst(criteria);
        }
        else
        {
            result = automation.Discovery.FindElement(criteria, TimeSpan.FromMilliseconds(Math.Max(request.TimeoutMs, 0)));
        }

        if (result is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Element not found."));
        }

        return BuildElementResponse(session.SessionId, result);
    }

    public override async Task<ElementListResponse> FindAllElements(FindElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var criteria = request.Criteria?.ToDomainCriteria() ?? new UiSearchCriteria();
        IReadOnlyList<IUIElement> elements;

        if (!string.IsNullOrWhiteSpace(request.RuntimeId))
        {
            var root = ResolveElement(session.SessionId, request.RuntimeId);
            elements = root.FindAll(criteria);
        }
        else
        {
            elements = automation.Discovery.FindAllElements(criteria);
        }

        var response = new ElementListResponse
        {
            Result = ProtoResults.Success()
        };

        foreach (var element in elements)
        {
            response.Elements.Add(TrackAndMap(session.SessionId, element));
        }

        return response;
    }

    public override async Task<ElementResponse> WaitForElement(WaitForElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var criteria = request.Criteria?.ToDomainCriteria() ?? new UiSearchCriteria();
        var timeout = TimeSpan.FromMilliseconds(Math.Max(request.TimeoutMs, 0));
        var polling = Math.Max(request.PollingIntervalMs, 100);

        IUIElement? result;
        if (!string.IsNullOrWhiteSpace(request.RuntimeId))
        {
            var root = ResolveElement(session.SessionId, request.RuntimeId);
            var stopwatch = Stopwatch.StartNew();
            result = null;
            while (stopwatch.Elapsed < timeout && !context.CancellationToken.IsCancellationRequested)
            {
                result = root.FindFirst(criteria);
                if (result is not null)
                {
                    break;
                }

                await Task.Delay(polling, context.CancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            result = await automation.Discovery.WaitForElementAsync(criteria, timeout).ConfigureAwait(false);
        }

        if (result is null)
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Element did not appear within the timeout."));
        }

        return BuildElementResponse(session.SessionId, result);
    }

    public override async Task GetChildren(ElementRequest request, IServerStreamWriter<ElementResponse> responseStream, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        foreach (var child in element.Children)
        {
            await responseStream.WriteAsync(BuildElementResponse(session.SessionId, child)).ConfigureAwait(false);
        }
    }

    public override async Task GetDescendants(GetDescendantsRequest request, IServerStreamWriter<ElementResponse> responseStream, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var maxDepth = request.MaxDepth <= 0 ? int.MaxValue : request.MaxDepth;

        foreach (var descendant in automation.TreeWalker.GetDescendants(element, maxDepth))
        {
            await responseStream.WriteAsync(BuildElementResponse(session.SessionId, descendant)).ConfigureAwait(false);
        }
    }

    public override async Task<TreeSnapshotResponse> CaptureTree(CaptureTreeRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var root = string.IsNullOrWhiteSpace(request.RuntimeId)
            ? automation.Discovery.GetDesktopRoot()
            : ResolveElement(session.SessionId, request.RuntimeId);

        var snapshot = automation.TreeWalker.CaptureSnapshot(root, request.MaxDepth <= 0 ? int.MaxValue : request.MaxDepth);
        return snapshot.ToProtoResponse();
    }

    public override async Task<ActionResponse> Click(ClickRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();

        var point = element.ClickablePoint;
        if (request.Offset is not null)
        {
            var rect = element.BoundingRectangle;
            point = new DrawingPoint(rect.Left + request.Offset.X, rect.Top + request.Offset.Y);
        }

        await automation.InputProvider.MoveMouseAsync(point, context.CancellationToken).ConfigureAwait(false);

        var button = request.ClickType switch
        {
            ClickType.Left => MouseButton.Left,
            ClickType.Right => MouseButton.Right,
            ClickType.Middle => MouseButton.Middle,
            ClickType.Double => MouseButton.Left,
            _ => MouseButton.Left
        };

        var clickOptions = request.ClickType == ClickType.Double
            ? new ClickOptions { ClickCount = 2 }
            : null;

        await automation.InputProvider.ClickAsync(button, clickOptions, context.CancellationToken).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> DoubleClick(ElementRequest request, ServerCallContext context)
    {
        var clickRequest = new ClickRequest
        {
            RuntimeId = request.RuntimeId,
            ClickType = ClickType.Double
        };
        return await Click(clickRequest, context).ConfigureAwait(false);
    }

    public override async Task<ActionResponse> RightClick(ElementRequest request, ServerCallContext context)
    {
        var clickRequest = new ClickRequest
        {
            RuntimeId = request.RuntimeId,
            ClickType = ClickType.Right
        };
        return await Click(clickRequest, context).ConfigureAwait(false);
    }

    public override async Task<ActionResponse> TypeText(TypeTextRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Text))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "text is required."));
        }

        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();

        if (request.ClearFirst)
        {
            await element.SetValueAsync(string.Empty).ConfigureAwait(false);
        }

        await element.SetFocusAsync().ConfigureAwait(false);
        var options = new TextEntryOptions
        {
            DelayBetweenCharactersMs = request.DelayBetweenKeysMs > 0 ? request.DelayBetweenKeysMs : 20
        };

        await automation.InputProvider.TypeTextAsync(request.Text, options, context.CancellationToken).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> SetValue(SetValueRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();

        await element.SetValueAsync(request.Value ?? string.Empty).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Invoke(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await element.InvokeAsync().ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> SetFocus(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await element.SetFocusAsync().ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Scroll(ScrollRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        if (!element.TryGetPattern<IScrollPattern>(out var scrollPattern))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Element does not support scrolling."));
        }

        var stopwatch = Stopwatch.StartNew();
        var amount = Math.Max(request.Amount, 1);
        var (horizontal, vertical) = request.Direction switch
        {
            ScrollDirection.Up => (System.Windows.Automation.ScrollAmount.NoAmount, System.Windows.Automation.ScrollAmount.SmallDecrement),
            ScrollDirection.Down => (System.Windows.Automation.ScrollAmount.NoAmount, System.Windows.Automation.ScrollAmount.SmallIncrement),
            ScrollDirection.Left => (System.Windows.Automation.ScrollAmount.SmallDecrement, System.Windows.Automation.ScrollAmount.NoAmount),
            ScrollDirection.Right => (System.Windows.Automation.ScrollAmount.SmallIncrement, System.Windows.Automation.ScrollAmount.NoAmount),
            _ => (System.Windows.Automation.ScrollAmount.NoAmount, System.Windows.Automation.ScrollAmount.NoAmount)
        };

        for (var i = 0; i < amount; i++)
        {
            await scrollPattern.ScrollAsync(horizontal, vertical).ConfigureAwait(false);
        }

        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<PatternsResponse> GetPatterns(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var response = new PatternsResponse
        {
            Result = ProtoResults.Success()
        };
        response.Patterns.Add(element.SupportedPatterns.Select(p => p.ToString()));
        return response;
    }

    public override async Task<ValueResponse> GetValue(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        if (!element.TryGetPattern<IValuePattern>(out var valuePattern))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Element does not expose ValuePattern."));
        }

        return new ValueResponse
        {
            Result = ProtoResults.Success(),
            Value = valuePattern.Value,
            IsReadonly = valuePattern.IsReadOnly
        };
    }

    public override async Task<ToggleStateResponse> GetToggleState(ElementRequest request, ServerCallContext context)
    {
        var pattern = await GetPatternAsync<ITogglePattern>(request, context, "TogglePattern").ConfigureAwait(false);
        return new ToggleStateResponse
        {
            Result = ProtoResults.Success(),
            State = pattern.ToggleState.ToProto()
        };
    }

    public override async Task<ActionResponse> Toggle(ElementRequest request, ServerCallContext context)
    {
        var pattern = await GetPatternAsync<ITogglePattern>(request, context, "TogglePattern").ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        await pattern.ToggleAsync().ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Expand(ElementRequest request, ServerCallContext context)
    {
        var pattern = await GetPatternAsync<IExpandCollapsePattern>(request, context, "ExpandCollapsePattern").ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        await pattern.ExpandAsync().ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Collapse(ElementRequest request, ServerCallContext context)
    {
        var pattern = await GetPatternAsync<IExpandCollapsePattern>(request, context, "ExpandCollapsePattern").ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        await pattern.CollapseAsync().ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Select(ElementRequest request, ServerCallContext context)
    {
        var pattern = await GetPatternAsync<ISelectionItemPattern>(request, context, "SelectionItemPattern").ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        await pattern.SelectAsync().ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> SetForeground(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        var success = await automation.WindowManager.SetForegroundAsync(element).ConfigureAwait(false);
        if (!success)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Unable to set window to foreground."));
        }

        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Minimize(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await automation.WindowManager.MinimizeAsync(element).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Maximize(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await automation.WindowManager.MaximizeAsync(element).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Restore(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await automation.WindowManager.RestoreAsync(element).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> Close(ElementRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await automation.WindowManager.CloseAsync(element).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> MoveWindow(MoveWindowRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await automation.WindowManager.MoveAsync(element, request.X, request.Y).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ActionResponse> ResizeWindow(ResizeWindowRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        var stopwatch = Stopwatch.StartNew();
        await automation.WindowManager.ResizeAsync(element, request.Width, request.Height).ConfigureAwait(false);
        return ActionResponse(stopwatch.Elapsed);
    }

    public override async Task<ElementResponse> AttachToProcess(AttachProcessRequest request, ServerCallContext context)
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        IUIElement? element = null;
        if (request.ProcessId > 0)
        {
            element = automation.WindowManager.AttachToProcess(request.ProcessId);
        }
        else if (!string.IsNullOrWhiteSpace(request.ProcessName))
        {
            element = automation.WindowManager.AttachToProcess(request.ProcessName);
        }

        if (element is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Process window not found."));
        }

        return BuildElementResponse(session.SessionId, element);
    }

    public override async Task<ElementResponse> LaunchAndAttach(LaunchRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "executable_path is required."));
        }

        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = automation.WindowManager.LaunchAndAttach(request.ExecutablePath, request.Arguments);
        if (element is null)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Failed to launch or attach to the process."));
        }

        return BuildElementResponse(session.SessionId, element);
    }

    private async Task<(GrpcSessionContext session, IUIAutomationService automation)> GetAutomationAsync(ServerCallContext context)
    {
        var session = _sessionAccessor.Current ?? new GrpcSessionContext("local", "local-agent", "local-run");
        var automation = await _sessionManager.GetServiceAsync(session, context.CancellationToken).ConfigureAwait(false);
        return (session, automation);
    }

    private ElementResponse BuildElementResponse(string sessionId, IUIElement element)
    {
        var protoElement = TrackAndMap(sessionId, element);
        return new ElementResponse
        {
            Result = ProtoResults.Success(),
            Element = protoElement
        };
    }

    private UIAutomation.Element TrackAndMap(string sessionId, IUIElement element)
    {
        _elementRegistry.Track(sessionId, element);
        return element.ToProtoElement();
    }

    private IUIElement ResolveElement(string sessionId, string runtimeId)
    {
        if (!_elementRegistry.TryResolve(sessionId, runtimeId, out var element) || element is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Element '{runtimeId}' is not cached. Re-run discovery before performing actions."));
        }

        return element;
    }

    private async Task<TPattern> GetPatternAsync<TPattern>(ElementRequest request, ServerCallContext context, string patternName)
        where TPattern : class
    {
        var (session, automation) = await GetAutomationAsync(context).ConfigureAwait(false);
        var element = ResolveElement(session.SessionId, request.RuntimeId);
        if (!element.TryGetPattern<TPattern>(out var pattern) || pattern is null)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, $"Element does not expose {patternName}."));
        }

        return pattern;
    }

    private static ActionResponse ActionResponse(TimeSpan elapsed) => new()
    {
        Result = ProtoResults.Success(),
        ExecutionTimeMs = (int)elapsed.TotalMilliseconds
    };
}

