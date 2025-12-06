using Cascade.Body.Configuration;
using Cascade.Proto;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Drawing.Drawing2D;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using ProtoImageFormat = Cascade.Proto.ImageFormat;

namespace Cascade.Body.Vision;

public class MarkerService
{
    private readonly VisionOptions _options;

    public MarkerService(IOptions<VisionOptions> options)
    {
        _options = options.Value;
    }

    public Screenshot ApplyMarks(Screenshot baseScreenshot, IEnumerable<UIElement> elements)
    {
        if (baseScreenshot.Image.IsEmpty)
        {
            return baseScreenshot;
        }

        var marks = new List<Mark>(baseScreenshot.Marks);
        using var ms = new MemoryStream(baseScreenshot.Image.ToByteArray());
        using var bitmap = new Bitmap(ms);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int index = marks.Count + 1;
        foreach (var element in elements)
        {
            var mark = new Mark { ElementId = element.Id, Label = index.ToString() };
            marks.Add(mark);
            DrawMark(g, bitmap.Size, element.BoundingBox, mark.Label);
            index++;
        }

        using var output = new MemoryStream();
        bitmap.Save(output, DrawingImageFormat.Png);
        return new Screenshot
        {
            Image = Google.Protobuf.ByteString.CopyFrom(output.ToArray()),
            Format = ProtoImageFormat.Png,
            Marks = { marks }
        };
    }

    private void DrawMark(Graphics g, Size imageSize, NormalizedRectangle rect, string label)
    {
        var x = rect.X * imageSize.Width;
        var y = rect.Y * imageSize.Height;
        var width = rect.Width * imageSize.Width;
        var height = rect.Height * imageSize.Height;
        var cx = x + width / 2;
        var cy = y + height / 2;

        var radius = Math.Max(_options.FontSize, 12);
        var circleRect = new RectangleF((float)(cx - radius), (float)(cy - radius), radius * 2, radius * 2);

        using var brush = new SolidBrush(Color.FromArgb(220, Color.OrangeRed));
        using var pen = new Pen(Color.Black, _options.StrokeWidth);
        using var textBrush = new SolidBrush(Color.White);
        using var font = new Font(FontFamily.GenericSansSerif, _options.FontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        g.FillEllipse(brush, circleRect);
        g.DrawEllipse(pen, circleRect);

        var textSize = g.MeasureString(label, font);
        var tx = circleRect.X + (circleRect.Width - textSize.Width) / 2;
        var ty = circleRect.Y + (circleRect.Height - textSize.Height) / 2;
        g.DrawString(label, font, textBrush, (float)tx, (float)ty);
    }
}

