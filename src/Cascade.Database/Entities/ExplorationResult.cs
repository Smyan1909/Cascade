using Cascade.Database.Enums;

namespace Cascade.Database.Entities;

/// <summary>
/// Represents a result captured during exploration.
/// </summary>
public class ExplorationResult
{
    /// <summary>
    /// Unique identifier for the result.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the parent exploration session.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Type of result captured.
    /// </summary>
    public ExplorationResultType Type { get; set; }

    /// <summary>
    /// Title of the window (for Window type results).
    /// </summary>
    public string? WindowTitle { get; set; }

    /// <summary>
    /// JSON data about the element (for Element type results).
    /// </summary>
    public string? ElementData { get; set; }

    /// <summary>
    /// JSON data about action test results (for ActionTest type).
    /// </summary>
    public string? ActionTestResult { get; set; }

    /// <summary>
    /// JSON data about navigation path (for NavigationPath type).
    /// </summary>
    public string? NavigationPath { get; set; }

    /// <summary>
    /// Screenshot captured at this point.
    /// </summary>
    public byte[]? Screenshot { get; set; }

    /// <summary>
    /// Text extracted via OCR from the screenshot.
    /// </summary>
    public string? OcrText { get; set; }

    /// <summary>
    /// When this result was captured.
    /// </summary>
    public DateTime CapturedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent exploration session.
    /// </summary>
    public ExplorationSession Session { get; set; } = null!;
}

