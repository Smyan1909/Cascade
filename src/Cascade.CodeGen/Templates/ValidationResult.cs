namespace Cascade.CodeGen.Templates;

/// <summary>
/// Result of template validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the template is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors (if any).
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}

