using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.CodeGen.Compilation;

public sealed class CompilationOptions
{
    public string? AssemblyName { get; set; }
    public OutputKind OutputKind { get; set; } = OutputKind.DynamicallyLinkedLibrary;
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Release;
    public IReadOnlyList<string> References { get; set; } = Array.Empty<string>();
    public IReadOnlyList<Assembly> AssemblyReferences { get; set; } = Array.Empty<Assembly>();
    public bool IncludeDefaultReferences { get; set; } = true;
    public IReadOnlyList<string> PreprocessorSymbols { get; set; } = Array.Empty<string>();
    public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.CSharp12;
    public NullableContextOptions NullableContextOptions { get; set; } = NullableContextOptions.Enable;
    public bool TreatWarningsAsErrors { get; set; }
    public IReadOnlyList<string> SuppressedWarnings { get; set; } = Array.Empty<string>();
}

