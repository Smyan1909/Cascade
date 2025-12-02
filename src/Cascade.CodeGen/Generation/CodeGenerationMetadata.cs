namespace Cascade.CodeGen.Generation;

/// <summary>
/// Metadata about code generation.
/// </summary>
public class CodeGenerationMetadata
{
    /// <summary>
    /// When the code was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Version of the code generator that created this code.
    /// </summary>
    public string GeneratorVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Name of the template used for generation.
    /// </summary>
    public string TemplateUsed { get; set; } = string.Empty;

    /// <summary>
    /// Additional parameters used during generation.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

