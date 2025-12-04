namespace Cascade.UIAutomation.Models;

/// <summary>
/// Mirrors the profile definition issued by the SessionService.
/// </summary>
public sealed class VirtualDesktopProfile
{
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public int Dpi { get; init; } = 100;
    public bool EnableGpu { get; init; }

    public static VirtualDesktopProfile Default => new();
}


