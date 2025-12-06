using Cascade.Body.Configuration;
using Cascade.Body.Vision;
using Cascade.Proto;
using FluentAssertions;
using Xunit;

namespace Cascade.Body.Tests;

// OCR + vision integration against a synthetic image; uses Windows.Media.Ocr.
[Trait("Category", "vision-ocr")]
public class VisionOcrIntegrationTests
{
    [Fact]
    public async Task OcrAndMarker_WorkTogetherOnSampleImage()
    {
        using var bmp = new System.Drawing.Bitmap(200, 120);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.White);
            using var font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 28, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            g.DrawString("OK", font, System.Drawing.Brushes.Black, new System.Drawing.PointF(20, 40));
        }

        var ocr = new OcrService(TestHelpers.Options(new OcrOptions { Enabled = true, LanguageTag = "en-US" }), TestHelpers.Logger<OcrService>());
        var ocrResult = await ocr.ExtractFromBitmapAsync(bmp, CancellationToken.None);

        ocrResult.Text.Should().NotBeNullOrWhiteSpace();

        // Apply marks using OCR regions to ensure marker + OCR cooperate.
        var screenshot = new Screenshot
        {
            Image = Google.Protobuf.ByteString.CopyFrom(ImageToBytes(bmp)),
            Format = ImageFormat.Png
        };

        var marker = new MarkerService(TestHelpers.Options(new VisionOptions { EnableVisionOcr = true }));
        var marked = marker.ApplyMarks(screenshot, ocrResult.Regions.Select((r, i) => new UIElement
        {
            Id = $"ocr-{i}",
            Name = r.Text,
            ValueText = r.Text,
            BoundingBox = r.Bounds,
            PlatformSource = PlatformSource.Windows
        }));

        marked.Marks.Should().NotBeEmpty();
        marked.Image.Length.Should().BeGreaterThan(0);
    }

    private static byte[] ImageToBytes(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}

