using System.Drawing;

namespace Cascade.Vision.Capture;

/// <summary>
/// Helper class for selecting and manipulating screen regions.
/// </summary>
public static class RegionSelector
{
    /// <summary>
    /// Gets the bounds of the primary screen.
    /// </summary>
    public static Rectangle GetPrimaryScreenBounds()
    {
        var screen = Screen.PrimaryScreen;
        return screen?.Bounds ?? Rectangle.Empty;
    }

    /// <summary>
    /// Gets the bounds of a specific screen by index.
    /// </summary>
    /// <param name="screenIndex">The zero-based screen index.</param>
    /// <returns>The screen bounds, or empty if index is invalid.</returns>
    public static Rectangle GetScreenBounds(int screenIndex)
    {
        var screens = Screen.AllScreens;
        if (screenIndex < 0 || screenIndex >= screens.Length)
            return Rectangle.Empty;
        
        return screens[screenIndex].Bounds;
    }

    /// <summary>
    /// Gets the combined bounds of all screens (virtual desktop).
    /// </summary>
    public static Rectangle GetVirtualScreenBounds()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return Rectangle.Empty;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var screen in screens)
        {
            minX = Math.Min(minX, screen.Bounds.Left);
            minY = Math.Min(minY, screen.Bounds.Top);
            maxX = Math.Max(maxX, screen.Bounds.Right);
            maxY = Math.Max(maxY, screen.Bounds.Bottom);
        }

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Gets the number of available screens.
    /// </summary>
    public static int ScreenCount => Screen.AllScreens.Length;

    /// <summary>
    /// Gets the screen that contains the specified point.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <returns>The screen index, or -1 if not found.</returns>
    public static int GetScreenIndexFromPoint(Point point)
    {
        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i].Bounds.Contains(point))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Clamps a rectangle to be within a screen's bounds.
    /// </summary>
    /// <param name="region">The region to clamp.</param>
    /// <param name="screenBounds">The screen bounds to clamp to.</param>
    /// <returns>The clamped region.</returns>
    public static Rectangle ClampToScreen(Rectangle region, Rectangle screenBounds)
    {
        int x = Math.Max(region.X, screenBounds.X);
        int y = Math.Max(region.Y, screenBounds.Y);
        int right = Math.Min(region.Right, screenBounds.Right);
        int bottom = Math.Min(region.Bottom, screenBounds.Bottom);

        return new Rectangle(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    /// <summary>
    /// Expands a region by the specified padding.
    /// </summary>
    /// <param name="region">The region to expand.</param>
    /// <param name="padding">The padding to add on all sides.</param>
    /// <returns>The expanded region.</returns>
    public static Rectangle ExpandRegion(Rectangle region, int padding)
    {
        return new Rectangle(
            region.X - padding,
            region.Y - padding,
            region.Width + (padding * 2),
            region.Height + (padding * 2));
    }

    /// <summary>
    /// Shrinks a region by the specified amount.
    /// </summary>
    /// <param name="region">The region to shrink.</param>
    /// <param name="amount">The amount to shrink on all sides.</param>
    /// <returns>The shrunk region.</returns>
    public static Rectangle ShrinkRegion(Rectangle region, int amount)
    {
        int newWidth = Math.Max(0, region.Width - (amount * 2));
        int newHeight = Math.Max(0, region.Height - (amount * 2));
        return new Rectangle(
            region.X + amount,
            region.Y + amount,
            newWidth,
            newHeight);
    }

    /// <summary>
    /// Centers a region on a specific point.
    /// </summary>
    /// <param name="size">The size of the region.</param>
    /// <param name="center">The center point.</param>
    /// <returns>The centered region.</returns>
    public static Rectangle CenterOnPoint(Size size, Point center)
    {
        return new Rectangle(
            center.X - (size.Width / 2),
            center.Y - (size.Height / 2),
            size.Width,
            size.Height);
    }

    /// <summary>
    /// Gets the center point of a region.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <returns>The center point.</returns>
    public static Point GetCenter(Rectangle region)
    {
        return new Point(
            region.X + (region.Width / 2),
            region.Y + (region.Height / 2));
    }

    /// <summary>
    /// Checks if a region is valid (has positive dimensions).
    /// </summary>
    /// <param name="region">The region to check.</param>
    /// <returns>True if the region is valid.</returns>
    public static bool IsValidRegion(Rectangle region)
    {
        return region.Width > 0 && region.Height > 0;
    }

    /// <summary>
    /// Scales a region by a factor.
    /// </summary>
    /// <param name="region">The region to scale.</param>
    /// <param name="factor">The scale factor.</param>
    /// <returns>The scaled region.</returns>
    public static Rectangle ScaleRegion(Rectangle region, double factor)
    {
        return new Rectangle(
            (int)(region.X * factor),
            (int)(region.Y * factor),
            (int)(region.Width * factor),
            (int)(region.Height * factor));
    }
}

