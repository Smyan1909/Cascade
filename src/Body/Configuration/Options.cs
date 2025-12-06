using Cascade.Proto;

namespace Cascade.Body.Configuration;

public class BodyOptions
{
    /// <summary>
    /// Default platform used when a request omits platform (e.g., GetSemanticTree()).
    /// </summary>
    public PlatformSource DefaultPlatform { get; set; } = PlatformSource.Windows;

    /// <summary>
    /// Optional default web URL to open when no app is specified for web.
    /// </summary>
    public string? DefaultUrl { get; set; }
}

public class UIA3Options
{
    public int ActionTimeoutMs { get; set; } = 8000;
    public int TreeDepth { get; set; } = 4;
    public int MaxNodes { get; set; } = 256;
}

public class PlaywrightOptions
{
    public bool Headless { get; set; } = true;
    public string? BrowserChannel { get; set; }
    public int ActionTimeoutMs { get; set; } = 8000;
    public int TreeDepth { get; set; } = 4;
    public int MaxNodes { get; set; } = 256;
}

public class VisionOptions
{
    public int MaxWidth { get; set; } = 1600;
    public int MaxHeight { get; set; } = 1200;
    public int FontSize { get; set; } = 18;
    public int StrokeWidth { get; set; } = 2;
    public bool EnableVisionOcr { get; set; } = false;
}

public class OcrOptions
{
    public bool Enabled { get; set; } = true;
    public string? LanguageTag { get; set; }
}

public static class PlatformSourceParser
{
    public static PlatformSource FromString(string? value, PlatformSource fallback = PlatformSource.Windows)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "WINDOWS" => PlatformSource.Windows,
            "JAVA" => PlatformSource.Java,
            "WEB" => PlatformSource.Web,
            _ => fallback
        };
    }
}

