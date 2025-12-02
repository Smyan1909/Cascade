namespace Cascade.CodeGen.Compiler;

/// <summary>
/// Represents a compilation error or warning.
/// </summary>
public class CompilationError
{
    /// <summary>
    /// Error code (e.g., "CS1001").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the error occurred (1-based).
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number where the error occurred (1-based).
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Optional file path where the error occurred.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Severity of the error.
    /// </summary>
    public CompilationErrorSeverity Severity { get; set; }
}

