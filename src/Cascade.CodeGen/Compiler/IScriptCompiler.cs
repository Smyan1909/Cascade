using Microsoft.CodeAnalysis;

namespace Cascade.CodeGen.Compiler;

/// <summary>
/// Interface for compiling C# source code.
/// </summary>
public interface IScriptCompiler
{
    /// <summary>
    /// Compiles source code into an assembly.
    /// </summary>
    Task<CompilationResult> CompileAsync(string sourceCode, CompilationOptions? options = null);

    /// <summary>
    /// Compiles multiple source files into an assembly.
    /// </summary>
    Task<CompilationResult> CompileFilesAsync(IEnumerable<string> filePaths, CompilationOptions? options = null);

    /// <summary>
    /// Evaluates a C# expression and returns a typed result.
    /// </summary>
    Task<ScriptResult<T>> EvaluateAsync<T>(string expression, ScriptGlobals? globals = null);

    /// <summary>
    /// Evaluates a C# script and returns the result.
    /// </summary>
    Task<ScriptResult> EvaluateAsync(string script, ScriptGlobals? globals = null);

    /// <summary>
    /// Checks syntax of source code and returns diagnostics.
    /// </summary>
    Task<IReadOnlyList<Diagnostic>> CheckSyntaxAsync(string sourceCode);

    /// <summary>
    /// Checks if source code has valid syntax.
    /// </summary>
    bool IsValidSyntax(string sourceCode);
}

/// <summary>
/// Result of script evaluation.
/// </summary>
public class ScriptResult
{
    public bool Success { get; set; }
    public object? ReturnValue { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Typed result of script evaluation.
/// </summary>
public class ScriptResult<T> : ScriptResult
{
    public new T? ReturnValue { get; set; }
}

/// <summary>
/// Global variables and objects available to scripts.
/// </summary>
public class ScriptGlobals
{
    public Dictionary<string, object> Variables { get; set; } = new();
}

