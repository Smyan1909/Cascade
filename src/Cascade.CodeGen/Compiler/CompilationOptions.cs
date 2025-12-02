using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.CodeGen.Compiler;

/// <summary>
/// Options for compiling C# source code.
/// </summary>
public class CompilationOptions
{
    /// <summary>
    /// Name of the output assembly.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Type of output to generate.
    /// </summary>
    public OutputKind OutputKind { get; set; } = OutputKind.DynamicallyLinkedLibrary;

    /// <summary>
    /// Optimization level for compilation.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Release;

    /// <summary>
    /// File paths to assembly references.
    /// </summary>
    public IReadOnlyList<string> References { get; set; } = new List<string>();

    /// <summary>
    /// Assembly references to include.
    /// </summary>
    public IReadOnlyList<Assembly> AssemblyReferences { get; set; } = new List<Assembly>();

    /// <summary>
    /// Whether to include default framework references.
    /// </summary>
    public bool IncludeDefaultReferences { get; set; } = true;

    /// <summary>
    /// Preprocessor symbols to define.
    /// </summary>
    public IReadOnlyList<string> PreprocessorSymbols { get; set; } = new List<string>();

    /// <summary>
    /// C# language version to use.
    /// </summary>
    public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Latest;

    /// <summary>
    /// Nullable context options.
    /// </summary>
    public NullableContextOptions NullableContextOptions { get; set; } = NullableContextOptions.Enable;

    /// <summary>
    /// Whether to treat warnings as errors.
    /// </summary>
    public bool TreatWarningsAsErrors { get; set; } = false;

    /// <summary>
    /// Warning codes to suppress.
    /// </summary>
    public IReadOnlyList<string> SuppressedWarnings { get; set; } = new List<string>();
}

