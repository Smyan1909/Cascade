using System.IO;
using Cascade.CodeGen.Generation;
using Cascade.Database.Entities;
using Cascade.Database.Enums;

namespace Cascade.CodeGen.Persistence;

/// <summary>
/// Extension methods for converting between Script entities and GeneratedCode.
/// </summary>
public static class ScriptExtensions
{
    /// <summary>
    /// Converts a GeneratedCode to a Script entity.
    /// </summary>
    public static Script ToScript(this GeneratedCode generatedCode, string name, string description, ScriptType type, Guid? agentId = null)
    {
        return new Script
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            SourceCode = generatedCode.SourceCode,
            CurrentVersion = "1.0.0",
            Type = type,
            TypeName = $"{generatedCode.Namespace}.{Path.GetFileNameWithoutExtension(generatedCode.FileName)}",
            MethodName = "ExecuteAsync",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AgentId = agentId,
            Metadata = new Dictionary<string, string>
            {
                ["GeneratedAt"] = generatedCode.Metadata.GeneratedAt.ToString("O"),
                ["TemplateUsed"] = generatedCode.Metadata.TemplateUsed,
                ["GeneratorVersion"] = generatedCode.Metadata.GeneratorVersion
            }
        };
    }

    /// <summary>
    /// Converts a Script entity to GeneratedCode.
    /// </summary>
    public static GeneratedCode ToGeneratedCode(this Script script)
    {
        return new GeneratedCode
        {
            SourceCode = script.SourceCode,
            FileName = $"{script.Name}.cs",
            Namespace = ExtractNamespace(script.TypeName),
            RequiredUsings = Array.Empty<string>(),
            RequiredReferences = Array.Empty<string>(),
            Metadata = new CodeGenerationMetadata
            {
                GeneratedAt = script.CreatedAt,
                GeneratorVersion = script.Metadata.TryGetValue("GeneratorVersion", out var version) ? version : "1.0.0",
                TemplateUsed = script.Metadata.TryGetValue("TemplateUsed", out var template) ? template : "",
                Parameters = new Dictionary<string, object>()
            }
        };
    }

    private static string ExtractNamespace(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "Cascade.Generated";

        var lastDot = typeName.LastIndexOf('.');
        if (lastDot < 0)
            return "Cascade.Generated";

        return typeName.Substring(0, lastDot);
    }
}

