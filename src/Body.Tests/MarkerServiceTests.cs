using Cascade.Body.Configuration;
using Cascade.Body.Vision;
using Cascade.Proto;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Drawing;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using ProtoImageFormat = Cascade.Proto.ImageFormat;
using Xunit;

namespace Cascade.Body.Tests;

public class MarkerServiceTests
{
    [Fact]
    public void ApplyMarks_DrawsMarksForEachElement()
    {
        using var bmp = new Bitmap(100, 100);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);

        using var ms = new MemoryStream();
        bmp.Save(ms, DrawingImageFormat.Png);

        var screenshot = new Screenshot
        {
            Image = Google.Protobuf.ByteString.CopyFrom(ms.ToArray()),
            Format = ProtoImageFormat.Png
        };

        var elements = new List<UIElement>
        {
            new() { Id = "a", BoundingBox = new NormalizedRectangle { X = 0.1, Y = 0.1, Width = 0.2, Height = 0.2 } },
            new() { Id = "b", BoundingBox = new NormalizedRectangle { X = 0.6, Y = 0.6, Width = 0.2, Height = 0.2 } }
        };

        var marker = new MarkerService(Options.Create(new VisionOptions()));
        var marked = marker.ApplyMarks(screenshot, elements);

        marked.Marks.Should().HaveCount(2);
        marked.Image.IsEmpty.Should().BeFalse();
    }
}

