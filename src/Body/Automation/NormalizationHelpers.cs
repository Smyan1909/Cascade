using Cascade.Proto;
using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Cascade.Body.Automation;

public static class NormalizationHelpers
{
    public static NormalizedRectangle ToNormalizedRectangle(RectangleF rect)
    {
        var screen = WinForms.Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        var width = Math.Max(1d, screen.Width);
        var height = Math.Max(1d, screen.Height);

        return new NormalizedRectangle
        {
            X = Clamp(rect.X / width),
            Y = Clamp(rect.Y / height),
            Width = Clamp(rect.Width / width),
            Height = Clamp(rect.Height / height)
        };
    }

    private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
}

