namespace Cascade.Database.Entities;

/// <summary>
/// Represents a version snapshot of a script.
/// </summary>
public class ScriptVersion
{
    /// <summary>
    /// Unique identifier for the version.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the parent script.
    /// </summary>
    public Guid ScriptId { get; set; }

    /// <summary>
    /// Version string (e.g., "1.0.0", "1.1.0").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The source code at this version.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of changes in this version.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// Compiled assembly bytes for this version (if available).
    /// </summary>
    public byte[]? CompiledAssembly { get; set; }

    /// <summary>
    /// When this version was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent script.
    /// </summary>
    public Script Script { get; set; } = null!;
}

