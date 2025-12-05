using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cascade.CodeGen.Templates;

/// <summary>
/// Provides strongly-typed context information that becomes available to Scriban templates.
/// </summary>
public sealed class TemplateContext
{
    private static readonly Regex PascalCaseRegex = new("(^|_)([a-z])", RegexOptions.Compiled);
    private static readonly Regex SnakeCaseRegex = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);

    public Dictionary<string, object> Variables { get; } = new();
    public string Namespace { get; set; } = "Cascade.Generated";
    public string ClassName { get; set; } = "GeneratedScript";
    public IReadOnlyList<string> Usings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> References { get; set; } = Array.Empty<string>();

    public Func<string, string> ToCamelCase { get; }
    public Func<string, string> ToPascalCase { get; }
    public Func<string, string> ToSnakeCase { get; }
    public Func<object, string> ToJson { get; }

    public TemplateContext()
    {
        ToCamelCase = value =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = ToPascal(value);
            return char.ToLowerInvariant(value[0]) + value[1..];
        };

        ToPascalCase = ToPascal;
        ToSnakeCase = value => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : SnakeCaseRegex.Replace(value, "$1_$2").ToLowerInvariant();
        ToJson = value => JsonSerializer.Serialize(value);
    }

    private static string ToPascal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = PascalCaseRegex.Replace(value, match => match.Groups[2].Value.ToUpperInvariant());
        return char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }
}

/// <summary>
/// Central place for configuring default namespaces/usings for template rendering.
/// </summary>
public sealed class TemplateContextFactory
{
    private readonly CodeGenOptions _options;

    public TemplateContextFactory(CodeGenOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public TemplateContext Create(string? ns = null, string? className = null)
    {
        return new TemplateContext
        {
            Namespace = string.IsNullOrWhiteSpace(ns) ? _options.DefaultNamespace : ns!,
            ClassName = string.IsNullOrWhiteSpace(className) ? "GeneratedScript" : className!,
            Usings = new[]
            {
                "System",
                "System.Threading",
                "System.Threading.Tasks",
                "Cascade.Core",
                "Cascade.Core.Session",
                "Cascade.UIAutomation.Discovery",
                "Cascade.CodeGen.Execution",
                "Cascade.UIAutomation.Elements"
            }
        };
    }
}

/// <summary>
/// Represents the result of validating a template.
/// </summary>
public sealed record TemplateValidationResult(bool Success, string? ErrorMessage = null)
{
    public static TemplateValidationResult Ok() => new(true, null);
    public static TemplateValidationResult Fail(string message) => new(false, message);
}

