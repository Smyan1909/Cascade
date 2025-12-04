namespace Cascade.Vision.Capture;

public static class RegionSelector
{
    public static Rectangle ClampToBounds(Rectangle region, Rectangle bounds)
    {
        var x = Math.Clamp(region.X, bounds.Left, bounds.Right);
        var y = Math.Clamp(region.Y, bounds.Top, bounds.Bottom);
        var width = Math.Clamp(region.Width, 1, bounds.Right - x);
        var height = Math.Clamp(region.Height, 1, bounds.Bottom - y);
        return new Rectangle(x, y, width, height);
    }

    public static Rectangle Expand(Rectangle region, int padding)
    {
        return new Rectangle(
            region.X - padding,
            region.Y - padding,
            region.Width + padding * 2,
            region.Height + padding * 2);
    }

    public static Rectangle FromElement(IUIElement element, int padding = 0)
    {
        var region = element?.BoundingRectangle ?? Rectangle.Empty;
        return padding > 0 ? Expand(region, padding) : region;
    }
}


