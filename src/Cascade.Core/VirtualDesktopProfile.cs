namespace Cascade.Core;

/// <summary>
/// Describes the hidden desktop profile that the Session Host should provision.
/// </summary>
public class VirtualDesktopProfile
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Dpi { get; set; } = 100;
    public bool EnableGpu { get; set; } = false;

    public static VirtualDesktopProfile Default => new();
}



