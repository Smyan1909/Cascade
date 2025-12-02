using System.Reflection;

namespace Cascade.CodeGen.Compiler;

/// <summary>
/// Result of a code compilation operation.
/// </summary>
public class CompilationResult
{
    /// <summary>
    /// Whether compilation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Compiled assembly as byte array.
    /// </summary>
    public byte[]? AssemblyBytes { get; set; }

    /// <summary>
    /// Compiled assembly object (if loaded).
    /// </summary>
    public Assembly? Assembly { get; set; }

    /// <summary>
    /// List of compilation errors.
    /// </summary>
    public IReadOnlyList<CompilationError> Errors { get; set; } = new List<CompilationError>();

    /// <summary>
    /// List of compilation warnings.
    /// </summary>
    public IReadOnlyList<CompilationWarning> Warnings { get; set; } = new List<CompilationWarning>();

    /// <summary>
    /// Time taken to compile.
    /// </summary>
    public TimeSpan CompilationTime { get; set; }

    /// <summary>
    /// Creates an instance of a type from the compiled assembly.
    /// </summary>
    /// <typeparam name="T">Type to create (must be a class).</typeparam>
    /// <param name="typeName">Fully qualified type name (e.g., "Namespace.ClassName").</param>
    /// <returns>Instance of the type, or null if not found.</returns>
    public T? CreateInstance<T>(string typeName) where T : class
    {
        if (Assembly == null || !Success)
            return null;

        var type = Assembly.GetType(typeName);
        if (type == null)
            return null;

        var instance = Activator.CreateInstance(type);
        return instance as T;
    }

    /// <summary>
    /// Creates an instance of a type from the compiled assembly.
    /// </summary>
    /// <param name="typeName">Fully qualified type name (e.g., "Namespace.ClassName").</param>
    /// <returns>Instance of the type, or null if not found.</returns>
    public object? CreateInstance(string typeName)
    {
        if (Assembly == null || !Success)
            return null;

        var type = Assembly.GetType(typeName);
        if (type == null)
            return null;

        return Activator.CreateInstance(type);
    }

    /// <summary>
    /// Gets a method from the compiled assembly.
    /// </summary>
    /// <param name="typeName">Fully qualified type name.</param>
    /// <param name="methodName">Name of the method.</param>
    /// <returns>MethodInfo, or null if not found.</returns>
    public MethodInfo? GetMethod(string typeName, string methodName)
    {
        if (Assembly == null || !Success)
            return null;

        var type = Assembly.GetType(typeName);
        if (type == null)
            return null;

        return type.GetMethod(methodName);
    }
}

