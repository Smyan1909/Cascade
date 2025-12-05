using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Cascade.CodeGen.Compilation;

public sealed class CompilationResult
{
    public bool Success { get; set; }
    public byte[]? AssemblyBytes { get; set; }
    public Assembly? Assembly { get; set; }
    public IReadOnlyList<CompilationError> Errors { get; set; } = Array.Empty<CompilationError>();
    public IReadOnlyList<CompilationError> Warnings { get; set; } = Array.Empty<CompilationError>();
    public TimeSpan CompilationTime { get; set; }

    public T? CreateInstance<T>(string typeName) where T : class
    {
        return CreateInstance(typeName) as T;
    }

    public object? CreateInstance(string typeName)
    {
        var assembly = Assembly ?? (AssemblyBytes is null ? null : Assembly.Load(AssemblyBytes));
        var type = assembly?.GetType(typeName, throwOnError: false, ignoreCase: false);
        return type is null ? null : Activator.CreateInstance(type);
    }

    public MethodInfo? GetMethod(string typeName, string methodName)
    {
        var assembly = Assembly ?? (AssemblyBytes is null ? null : Assembly.Load(AssemblyBytes));
        var type = assembly?.GetType(typeName, throwOnError: false, ignoreCase: false);
        return type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
    }
}

public sealed class CompilationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string? FilePath { get; set; }
    public DiagnosticSeverity Severity { get; set; }
}

