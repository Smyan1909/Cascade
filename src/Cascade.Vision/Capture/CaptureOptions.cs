namespace Cascade.Vision.Capture;

public class CaptureOptions
{
    public string ImageFormat { get; set; } = "png";
    public int JpegQuality { get; set; } = 90;
    public bool IncludeCursor { get; set; }
    public bool CaptureShadow { get; set; }
    public double Scale { get; set; } = 1.0;
    public Rectangle? CropRegion { get; set; }
    public bool RemoveTransparency { get; set; } = true;
    public Color TransparencyReplacement { get; set; } = Color.White;
    public bool UseVirtualDisplayDuplication { get; set; } = true;
}


