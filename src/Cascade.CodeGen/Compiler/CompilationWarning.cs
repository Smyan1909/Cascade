namespace Cascade.CodeGen.Compiler;

/// <summary>
/// Represents a compilation warning.
/// </summary>
public class CompilationWarning
{
    /// <summary>
    /// Warning code (e.g., "CS0168").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable warning message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the warning occurred (1-based).
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number where the warning occurred (1-based).
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Optional file path where the warning occurred.
    /// </summary>
    public string? FilePath { get; set; }
}

