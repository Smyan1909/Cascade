using System.Drawing;

namespace Cascade.Vision.Capture;

/// <summary>
/// Configuration options for screen capture operations.
/// </summary>
public class CaptureOptions
{
    /// <summary>
    /// Gets or sets the image format for captured images.
    /// Supported formats: "png", "jpeg", "bmp".
    /// </summary>
    public string ImageFormat { get; set; } = "png";

    /// <summary>
    /// Gets or sets the JPEG quality (1-100) when using JPEG format.
    /// </summary>
    public int JpegQuality { get; set; } = 90;

    /// <summary>
    /// Gets or sets whether to include the mouse cursor in the capture.
    /// </summary>
    public bool IncludeCursor { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to capture window shadow (Windows Aero).
    /// </summary>
    public bool CaptureShadow { get; set; } = false;

    /// <summary>
    /// Gets or sets the scale factor for the captured image.
    /// Values > 1.0 upscale, values &lt; 1.0 downscale.
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets an optional region to crop from the capture.
    /// </summary>
    public Rectangle? CropRegion { get; set; }

    /// <summary>
    /// Gets or sets whether to remove transparency from captured images.
    /// </summary>
    public bool RemoveTransparency { get; set; } = true;

    /// <summary>
    /// Gets or sets the color to replace transparent pixels with.
    /// </summary>
    public Color TransparencyReplacement { get; set; } = Color.White;

    /// <summary>
    /// Gets or sets whether to capture the client area only (excluding window borders).
    /// </summary>
    public bool ClientAreaOnly { get; set; } = false;

    /// <summary>
    /// Creates a default CaptureOptions instance.
    /// </summary>
    public static CaptureOptions Default => new();

    /// <summary>
    /// Creates options optimized for OCR processing.
    /// </summary>
    public static CaptureOptions ForOcr => new()
    {
        ImageFormat = "png",
        Scale = 2.0, // Upscale for better OCR accuracy
        RemoveTransparency = true,
        TransparencyReplacement = Color.White
    };

    /// <summary>
    /// Creates options optimized for fast capture with minimal processing.
    /// </summary>
    public static CaptureOptions Fast => new()
    {
        ImageFormat = "bmp",
        Scale = 1.0,
        IncludeCursor = false,
        CaptureShadow = false
    };
}

