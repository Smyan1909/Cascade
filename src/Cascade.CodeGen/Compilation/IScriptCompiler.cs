namespace Cascade.CodeGen.Compilation;

public interface IScriptCompiler
{
    Task<CompilationResult> CompileAsync(string sourceCode, CompilationOptions? options = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic>> CheckSyntaxAsync(string sourceCode, CancellationToken cancellationToken = default);
    bool IsValidSyntax(string sourceCode);
}

