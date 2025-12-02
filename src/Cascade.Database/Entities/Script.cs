using Cascade.Database.Enums;

namespace Cascade.Database.Entities;

/// <summary>
/// Represents a C# script that can be compiled and executed.
/// </summary>
public class Script
{
    /// <summary>
    /// Unique identifier for the script.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the script.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the script does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The C# source code of the script.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Current version string.
    /// </summary>
    public string CurrentVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Type of script (Action, Workflow, Agent, etc.).
    /// </summary>
    public ScriptType Type { get; set; }

    /// <summary>
    /// Compiled assembly bytes (cached for performance).
    /// </summary>
    public byte[]? CompiledAssembly { get; set; }

    /// <summary>
    /// When the script was last compiled.
    /// </summary>
    public DateTime? LastCompiledAt { get; set; }

    /// <summary>
    /// Any compilation errors from the last compile attempt.
    /// </summary>
    public string? CompilationErrors { get; set; }

    /// <summary>
    /// Additional metadata key-value pairs.
    /// Stored as JSON.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// The fully qualified type name of the main class.
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// The method name to invoke for execution.
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// When the script was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the script was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Optional foreign key to the owning agent.
    /// </summary>
    public Guid? AgentId { get; set; }

    // Navigation properties

    /// <summary>
    /// The owning agent (if any).
    /// </summary>
    public Agent? Agent { get; set; }

    /// <summary>
    /// Collection of version snapshots for this script.
    /// </summary>
    public ICollection<ScriptVersion> Versions { get; set; } = new List<ScriptVersion>();
}

