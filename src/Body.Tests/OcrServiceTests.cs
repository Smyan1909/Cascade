using Cascade.Body.Configuration;
using Cascade.Body.Vision;
using FluentAssertions;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace Cascade.Body.Tests;

public class OcrServiceTests
{
    [Fact]
    public async Task ExtractFromBitmapAsync_ReturnsText()
    {
        using var bmp = new Bitmap(200, 80);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var font = new Font(FontFamily.GenericSansSerif, 28, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString("Hello", font, Brushes.Black, new PointF(10, 20));

        var ocr = new OcrService(TestHelpers.Options(new OcrOptions { Enabled = true, LanguageTag = "en-US" }), TestHelpers.Logger<OcrService>());
        var result = await ocr.ExtractFromBitmapAsync(bmp, CancellationToken.None);

        result.Text.Should().NotBeNull();
        result.Text!.Length.Should().BeGreaterThan(0);
    }
}

