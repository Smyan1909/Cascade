namespace Cascade.CodeGen.Generation;

/// <summary>
/// Represents generated source code and its metadata.
/// </summary>
public class GeneratedCode
{
    /// <summary>
    /// The generated C# source code.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Suggested file name for the generated code.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The namespace for the generated code.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Required using statements for the generated code.
    /// </summary>
    public IReadOnlyList<string> RequiredUsings { get; set; } = new List<string>();

    /// <summary>
    /// Required assembly references for compilation.
    /// </summary>
    public IReadOnlyList<string> RequiredReferences { get; set; } = new List<string>();

    /// <summary>
    /// Metadata about the code generation process.
    /// </summary>
    public CodeGenerationMetadata Metadata { get; set; } = new();
}

