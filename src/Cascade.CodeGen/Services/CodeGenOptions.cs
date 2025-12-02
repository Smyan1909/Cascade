using Cascade.CodeGen.Compiler;
using Cascade.CodeGen.Execution;

namespace Cascade.CodeGen.Services;

/// <summary>
/// Configuration options for the CodeGen service.
/// </summary>
public class CodeGenOptions
{
    // Templates
    public string? TemplateDirectory { get; set; }
    public IReadOnlyList<string> AdditionalTemplates { get; set; } = new List<string>();

    // Compilation
    public CompilationOptions DefaultCompilationOptions { get; set; } = new();
    public bool CacheCompilations { get; set; } = true;
    public int MaxCachedCompilations { get; set; } = 100;

    // Execution
    public SecurityPolicy DefaultSecurityPolicy { get; set; } = SecurityPolicy.Default;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    // Code generation
    public string DefaultNamespace { get; set; } = "Cascade.Generated";
    public bool IncludeDebugInfo { get; set; } = true;
    public bool OptimizeGeneratedCode { get; set; } = true;
}

