using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Cascade.Tests.Vision;

internal static class TestImageFactory
{
    public static byte[] CreateSolidColor(Color color, int width = 100, int height = 50)
    {
        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(color);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}


