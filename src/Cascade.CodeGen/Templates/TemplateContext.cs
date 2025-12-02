using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cascade.CodeGen.Templates;

/// <summary>
/// Context for template rendering with helper methods.
/// </summary>
public class TemplateContext
{
    /// <summary>
    /// Variables available in the template.
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>
    /// Namespace for generated code.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Class name for generated code.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Required using statements.
    /// </summary>
    public IReadOnlyList<string> Usings { get; set; } = new List<string>();

    /// <summary>
    /// Required assembly references.
    /// </summary>
    public IReadOnlyList<string> References { get; set; } = new List<string>();

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    public Func<string, string> ToCamelCase { get; } = (s) =>
    {
        if (string.IsNullOrEmpty(s))
            return s;
        if (s.Length == 1)
            return s.ToLowerInvariant();
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    };

    /// <summary>
    /// Converts a string to PascalCase.
    /// </summary>
    public Func<string, string> ToPascalCase { get; } = (s) =>
    {
        if (string.IsNullOrEmpty(s))
            return s;
        if (s.Length == 1)
            return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    };

    /// <summary>
    /// Converts a string to snake_case.
    /// </summary>
    public Func<string, string> ToSnakeCase { get; } = (s) =>
    {
        if (string.IsNullOrEmpty(s))
            return s;

        return Regex.Replace(s, @"([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
    };

    /// <summary>
    /// Converts an object to JSON string.
    /// </summary>
    public Func<object, string> ToJson { get; } = (obj) =>
    {
        if (obj == null)
            return "null";
        return JsonSerializer.Serialize(obj);
    };
}

