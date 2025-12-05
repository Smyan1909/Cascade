using Cascade.Core.Session;
using Cascade.Grpc.Server.Mappers;
using Cascade.Grpc.Server.Sessions;
using Cascade.Grpc.Vision;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Services;
using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using Cascade.Vision.Services;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using VisionBackendService = Cascade.Vision.Services.VisionService;
using VisionProtoService = Cascade.Grpc.Vision.VisionService;
using DomainCaptureOptions = Cascade.Vision.Capture.CaptureOptions;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Cascade.Grpc.Server.Services;

public sealed class VisionGrpcService : VisionProtoService.VisionServiceBase
{
    private readonly VisionBackendService _visionService;
    private readonly ISessionRuntimeResolver _runtimeResolver;
    private readonly IGrpcSessionContextAccessor _sessionAccessor;
    private readonly UiElementRegistry _elementRegistry;
    private readonly IChangeDetector _changeDetector;
    private readonly ILogger<VisionGrpcService> _logger;

    public VisionGrpcService(
        VisionBackendService visionService,
        ISessionRuntimeResolver runtimeResolver,
        IGrpcSessionContextAccessor sessionAccessor,
        UiElementRegistry elementRegistry,
        IChangeDetector changeDetector,
        ILogger<VisionGrpcService> logger)
    {
        _visionService = visionService ?? throw new ArgumentNullException(nameof(visionService));
        _runtimeResolver = runtimeResolver ?? throw new ArgumentNullException(nameof(runtimeResolver));
        _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));
        _elementRegistry = elementRegistry ?? throw new ArgumentNullException(nameof(elementRegistry));
        _changeDetector = changeDetector ?? throw new ArgumentNullException(nameof(changeDetector));
        _logger = logger;
    }

    public override async Task<CaptureResponse> CaptureScreen(CaptureScreenRequest request, ServerCallContext context)
    {
        var (handle, captureOptions) = await ResolveSessionAsync(request.Options, context).ConfigureAwait(false);
        var capture = await _visionService.CaptureScreenAsync(handle, request.ScreenIndex, captureOptions, context.CancellationToken).ConfigureAwait(false);
        return capture.ToProto();
    }

    public override async Task<CaptureResponse> CaptureWindow(CaptureWindowRequest request, ServerCallContext context)
    {
        var (handle, captureOptions) = await ResolveSessionAsync(request.Options, context).ConfigureAwait(false);
        CaptureResult capture;

        if (!string.IsNullOrWhiteSpace(request.WindowHandle) &&
            long.TryParse(request.WindowHandle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var handleValue))
        {
            capture = await _visionService.CaptureWindowAsync(handle, new IntPtr(handleValue), captureOptions, context.CancellationToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(request.WindowTitle))
        {
            capture = await _visionService.CaptureWindowAsync(handle, request.WindowTitle, captureOptions, context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            capture = await _visionService.CaptureForegroundWindowAsync(handle, captureOptions, context.CancellationToken).ConfigureAwait(false);
        }

        return capture.ToProto();
    }

    public override async Task<CaptureResponse> CaptureElement(CaptureElementRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.RuntimeId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "runtime_id is required."));
        }

        var (handle, captureOptions) = await ResolveSessionAsync(request.Options, context).ConfigureAwait(false);
        var element = ResolveElement(handle.SessionId.ToString(), request.RuntimeId);
        var capture = await _visionService.CaptureElementAsync(handle, element.BoundingRectangle, captureOptions, context.CancellationToken).ConfigureAwait(false);
        return capture.ToProto();
    }

    public override async Task<CaptureResponse> CaptureRegion(CaptureRegionRequest request, ServerCallContext context)
    {
        var (handle, captureOptions) = await ResolveSessionAsync(request.Options, context).ConfigureAwait(false);
        var rect = request.Region ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "region is required."));
        var capture = await _visionService.CaptureRegionAsync(handle, new DrawingRectangle(rect.X, rect.Y, rect.Width, rect.Height), captureOptions, context.CancellationToken).ConfigureAwait(false);
        return capture.ToProto();
    }

    public override async Task<OcrResponse> RecognizeText(RecognizeRequest request, ServerCallContext context)
    {
        var (handle, _) = await ResolveSessionAsync(null, context).ConfigureAwait(false);
        CaptureResult? capture = null;

        if (!request.ImageData.IsEmpty)
        {
            capture = BuildCaptureFromBytes(handle.SessionId, request.ImageData);
        }

        var result = await _visionService.RecognizeAsync(handle, capture, context.CancellationToken).ConfigureAwait(false);
        return result.ToProto();
    }

    public override async Task<FindTextResponse> FindText(FindTextRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.SearchText))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "search_text is required."));
        }

        var recognizeRequest = new RecognizeRequest
        {
            ImageData = request.ImageData
        };

        var ocr = await RecognizeText(recognizeRequest, context).ConfigureAwait(false);
        var comparison = request.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matches = new FindTextResponse
        {
            Result = ProtoResults.Success(),
            Found = false
        };

        foreach (var line in ocr.Lines)
        {
            foreach (var word in line.Words)
            {
                if (word.Text?.Contains(request.SearchText, comparison) == true)
                {
                    matches.Found = true;
                    matches.Matches.Add(new TextMatch
                    {
                        Text = word.Text,
                        BoundingBox = word.BoundingBox,
                        Confidence = word.Confidence
                    });
                }
            }
        }

        return matches;
    }

    public override async Task<Result> SetBaseline(SetBaselineRequest request, ServerCallContext context)
    {
        if (request.ImageData.IsEmpty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "image_data is required."));
        }

        await _changeDetector.SetBaselineAsync(request.ImageData.ToByteArray(), context.CancellationToken).ConfigureAwait(false);
        return ProtoResults.Success();
    }

    public override async Task<ChangeResponse> CompareWithBaseline(CompareRequest request, ServerCallContext context)
    {
        if (request.ImageData.IsEmpty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "image_data is required."));
        }

        var result = await _changeDetector.CompareWithBaselineAsync(request.ImageData.ToByteArray(), context.CancellationToken).ConfigureAwait(false);
        return result.ToProto();
    }

    public override async Task<ChangeResponse> CompareImages(CompareImagesRequest request, ServerCallContext context)
    {
        if (request.BaselineImage.IsEmpty || request.CurrentImage.IsEmpty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Both baseline_image and current_image are required."));
        }

        var result = await _changeDetector.CompareAsync(request.BaselineImage.ToByteArray(), request.CurrentImage.ToByteArray(), context.CancellationToken).ConfigureAwait(false);
        return result.ToProto();
    }

    public override async Task<ChangeResponse> WaitForChange(WaitForChangeRequest request, ServerCallContext context)
    {
        var (handle, _) = await ResolveSessionAsync(null, context).ConfigureAwait(false);
        var region = request.Region ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "region is required."));
        var timeout = TimeSpan.FromMilliseconds(Math.Max(request.TimeoutMs, 1000));

        var result = await _visionService.WaitForChangeAsync(
            handle,
            new DrawingRectangle(region.X, region.Y, region.Width, region.Height),
            timeout,
            context.CancellationToken).ConfigureAwait(false);

        return result.ToProto();
    }

    public override async Task<LayoutResponse> AnalyzeLayout(AnalyzeLayoutRequest request, ServerCallContext context)
    {
        var (handle, _) = await ResolveSessionAsync(null, context).ConfigureAwait(false);
        CaptureResult? capture = null;
        if (!request.ImageData.IsEmpty)
        {
            capture = BuildCaptureFromBytes(handle.SessionId, request.ImageData);
        }

        var result = await _visionService.AnalyzeLayoutAsync(handle, capture, context.CancellationToken).ConfigureAwait(false);
        return result.ToProto();
    }

    public override async Task<VisualElementsResponse> DetectElements(DetectElementsRequest request, ServerCallContext context)
    {
        var (handle, _) = await ResolveSessionAsync(null, context).ConfigureAwait(false);
        CaptureResult? capture = null;
        if (!request.ImageData.IsEmpty)
        {
            capture = BuildCaptureFromBytes(handle.SessionId, request.ImageData);
        }

        var elements = await _visionService.AnalyzeElementsAsync(handle, capture, context.CancellationToken).ConfigureAwait(false);
        return elements.ToProto();
    }

    private async Task<(SessionHandle Handle, DomainCaptureOptions Options)> ResolveSessionAsync(Cascade.Grpc.Vision.CaptureOptions? options, ServerCallContext context)
    {
        var session = _sessionAccessor.Current ?? new GrpcSessionContext("local", "local-agent", "local-run");
        var runtime = await _runtimeResolver.ResolveAsync(session, context.CancellationToken).ConfigureAwait(false);
        return (runtime.Handle, options.ToDomain());
    }

    private IUIElement ResolveElement(string sessionId, string runtimeId)
    {
        if (!_elementRegistry.TryResolve(sessionId, runtimeId, out var element) || element is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Element '{runtimeId}' is not cached."));
        }

        return element;
    }

    private static CaptureResult BuildCaptureFromBytes(Guid sessionId, ByteString data)
    {
        var bytes = data.ToByteArray();
        using var stream = new MemoryStream(bytes);
        using var image = Image.FromStream(stream);
        var rect = new DrawingRectangle(0, 0, image.Width, image.Height);

        return new CaptureResult
        {
            SessionId = sessionId,
            ImageData = bytes,
            ImageFormat = "png",
            Width = image.Width,
            Height = image.Height,
            CapturedRegion = rect,
            CapturedAt = DateTime.UtcNow
        };
    }
}

