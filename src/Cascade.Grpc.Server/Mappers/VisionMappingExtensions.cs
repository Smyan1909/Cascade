using Cascade.Grpc.Vision;
using Cascade.Vision.Analysis;
using Cascade.Vision.Capture;
using Cascade.Vision.Comparison;
using Cascade.Vision.OCR;
using Google.Protobuf;
using System.Linq;
using ProtoCaptureOptions = Cascade.Grpc.Vision.CaptureOptions;
using ProtoCompareOptions = Cascade.Grpc.Vision.CompareOptions;
using ProtoRectangle = Cascade.Grpc.Rectangle;
using DomainCaptureOptions = Cascade.Vision.Capture.CaptureOptions;
using DomainComparisonOptions = Cascade.Vision.Comparison.ComparisonOptions;
using DomainVisualElement = Cascade.Vision.Analysis.VisualElement;
using ProtoOcrLine = Cascade.Grpc.Vision.OcrLine;
using ProtoOcrWord = Cascade.Grpc.Vision.OcrWord;
using ProtoLayoutRegion = Cascade.Grpc.Vision.LayoutRegion;
using ProtoVisualElement = Cascade.Grpc.Vision.VisualElement;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Cascade.Grpc.Server.Mappers;

internal static class VisionMappingExtensions
{
    public static CaptureResponse ToProto(this CaptureResult capture)
    {
        return new CaptureResponse
        {
            Result = ProtoResults.Success(),
            ImageData = ByteString.CopyFrom(capture.ImageData),
            Width = capture.Width,
            Height = capture.Height,
            Format = capture.ImageFormat,
            CapturedRegion = MapRectangle(capture.CapturedRegion)
        };
    }

    public static OcrResponse ToProto(this OcrResult result)
    {
        var response = new OcrResponse
        {
            Result = ProtoResults.Success(),
            FullText = result.FullText,
            Confidence = result.Confidence,
            ProcessingTimeMs = (int)result.ProcessingTime.TotalMilliseconds
        };

        foreach (var line in result.Lines)
        {
            var protoLine = new ProtoOcrLine
            {
                Text = line.Text,
                Confidence = line.Confidence,
                BoundingBox = MapRectangle(line.BoundingBox)
            };

            foreach (var word in line.Words)
            {
                protoLine.Words.Add(new ProtoOcrWord
                {
                    Text = word.Text,
                    Confidence = word.Confidence,
                    BoundingBox = MapRectangle(word.BoundingBox)
                });
            }

            response.Lines.Add(protoLine);
        }

        return response;
    }

    public static ChangeResponse ToProto(this ChangeResult result)
    {
        var response = new ChangeResponse
        {
            Result = ProtoResults.Success(),
            HasChanges = result.HasChanges,
            DifferencePercentage = result.DifferencePercentage,
            DifferenceImage = result.DifferenceImage is null ? ByteString.Empty : ByteString.CopyFrom(result.DifferenceImage)
        };

        response.ChangedRegions.Add(result.ChangedRegions.Select(MapRectangle));
        return response;
    }

    public static LayoutResponse ToProto(this LayoutAnalysis analysis)
    {
        var response = new LayoutResponse
        {
            Result = ProtoResults.Success(),
            LayoutType = analysis.DetectedLayout.ToString(),
            ContentArea = MapRectangle(analysis.ContentArea)
        };

        response.Regions.Add(analysis.Regions.Select(r => new ProtoLayoutRegion
        {
            Name = r.Name,
            Type = r.Type.ToString(),
            Bounds = MapRectangle(r.Bounds)
        }));

        return response;
    }

    public static VisualElementsResponse ToProto(this IReadOnlyList<DomainVisualElement> elements)
    {
        var response = new VisualElementsResponse
        {
            Result = ProtoResults.Success()
        };

        foreach (var element in elements)
        {
            response.Elements.Add(new ProtoVisualElement
            {
                Type = element.Type.ToString(),
                BoundingBox = MapRectangle(element.BoundingBox),
                Confidence = element.Confidence,
                Text = element.Text ?? string.Empty
            });
        }

        return response;
    }

    public static RecognizeRequest CloneWithImage(this RecognizeRequest request, CaptureResult capture)
    {
        return new RecognizeRequest
        {
            ImageData = ByteString.CopyFrom(capture.ImageData),
            Options = request.Options
        };
    }

    public static DomainCaptureOptions ToDomain(this ProtoCaptureOptions? options)
    {
        var domain = new DomainCaptureOptions();
        if (options is null)
        {
            return domain;
        }

        if (!string.IsNullOrWhiteSpace(options.Format))
        {
            domain.ImageFormat = options.Format;
        }

        if (options.JpegQuality > 0)
        {
            domain.JpegQuality = options.JpegQuality;
        }

        domain.IncludeCursor = options.IncludeCursor;
        domain.Scale = options.Scale > 0 ? options.Scale : domain.Scale;
        return domain;
    }

    public static DomainComparisonOptions ToDomain(this ProtoCompareOptions? options)
    {
        if (options is null)
        {
            return new DomainComparisonOptions();
        }

        var domain = new DomainComparisonOptions
        {
            ChangeThreshold = options.ChangeThreshold > 0 ? options.ChangeThreshold : 0.05,
            IgnoreAntiAliasing = options.IgnoreAntialiasing,
            ColorTolerance = options.ColorTolerance > 0 ? options.ColorTolerance : 15
        };

        if (options.IgnoreRegions != null && options.IgnoreRegions.Count > 0)
        {
            domain.IgnoreRegions = options.IgnoreRegions
                .Select(r => new DrawingRectangle(r.X, r.Y, r.Width, r.Height))
                .ToList();
        }

        return domain;
    }

    private static ProtoRectangle MapRectangle(DrawingRectangle rectangle)
    {
        return new ProtoRectangle
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height
        };
    }
}

