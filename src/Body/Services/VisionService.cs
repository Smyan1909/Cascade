using Cascade.Body.Automation;
using Cascade.Body.Configuration;
using Cascade.Body.Vision;
using Cascade.Proto;
using Grpc.Core;
using Microsoft.Extensions.Options;
using System.Linq;

namespace Cascade.Body.Services;

public class VisionService : Proto.VisionService.VisionServiceBase
{
    private readonly AutomationRouter _router;
    private readonly MarkerService _markerService;
    private readonly OcrService _ocrService;
    private readonly VisionOptions _visionOptions;

    public VisionService(AutomationRouter router, MarkerService markerService, OcrService ocrService, IOptions<VisionOptions> visionOptions)
    {
        _router = router;
        _markerService = markerService;
        _ocrService = ocrService;
        _visionOptions = visionOptions.Value;
    }

    public override async Task<Screenshot> GetMarkedScreenshot(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
    {
        var provider = _router.GetProvider(PlatformSource.Unspecified);
        if (provider is null)
        {
            return new Screenshot();
        }

        var screenshot = await provider.GetMarkedScreenshotAsync(context.CancellationToken).ConfigureAwait(false);
        if (screenshot.Marks.Count == 0 && screenshot.Image.Length > 0)
        {
            // If provider returned a screenshot without marks, add default tags from the tree when possible.
            var tree = await provider.GetSemanticTreeAsync(context.CancellationToken).ConfigureAwait(false);
            var marked = _markerService.ApplyMarks(screenshot, tree.Elements);
            screenshot = marked;
        }

        if (_visionOptions.EnableVisionOcr && _ocrService.IsEnabled && screenshot.Image.Length > 0 && screenshot.Marks.Count == 0)
        {
            var ocrResult = await _ocrService.ExtractAsync(screenshot.Image.ToByteArray(), context.CancellationToken).ConfigureAwait(false);
            if (ocrResult.Regions.Count > 0)
            {
                var ocrElements = ocrResult.Regions.Select((r, i) => new UIElement
                {
                    Id = $"ocr-{i + 1}",
                    Name = r.Text,
                    ValueText = r.Text,
                    BoundingBox = r.Bounds,
                        PlatformSource = PlatformSource.Windows
                });

                screenshot = _markerService.ApplyMarks(screenshot, ocrElements);
            }
        }

        return screenshot;
    }
}

