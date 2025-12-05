using Cascade.CodeGen.Execution;

namespace Cascade.CodeGen;

public sealed class CodeGenOptions
{
    public string DefaultNamespace { get; set; } = "Cascade.Generated";
    public string? TemplateDirectory { get; set; }
    public IReadOnlyList<string> AdditionalTemplates { get; set; } = Array.Empty<string>();
    public double DefaultActionTimeoutSeconds { get; set; } = 15;
    public bool CacheCompilations { get; set; } = true;
    public int MaxCachedCompilations { get; set; } = 64;
    public SecurityPolicy DefaultSecurityPolicy { get; set; } = SecurityPolicy.Default;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool RequireSessionContext { get; set; } = true;
    public bool AllowSessionRetry { get; set; } = true;
    public string GeneratorVersion { get; set; } = "1.0.0";
}

