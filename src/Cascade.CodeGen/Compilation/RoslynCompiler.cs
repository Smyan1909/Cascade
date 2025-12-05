using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.CodeGen.Compilation;

public sealed class RoslynCompiler : IScriptCompiler
{
    public async Task<CompilationResult> CompileAsync(string sourceCode, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code is required.", nameof(sourceCode));
        }

        var effectiveOptions = options ?? new CompilationOptions();
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(languageVersion: effectiveOptions.LanguageVersion));
        var references = new List<MetadataReference>();

        if (effectiveOptions.IncludeDefaultReferences)
        {
            references.AddRange(DefaultReferences.GetReferences());
        }

        foreach (var assembly in effectiveOptions.AssemblyReferences)
        {
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        foreach (var reference in effectiveOptions.References)
        {
            references.Add(MetadataReference.CreateFromFile(reference));
        }

        var compilation = CSharpCompilation.Create(
            effectiveOptions.AssemblyName ?? $"Cascade.Generated.{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                effectiveOptions.OutputKind,
                optimizationLevel: effectiveOptions.OptimizationLevel,
                nullableContextOptions: effectiveOptions.NullableContextOptions,
                generalDiagnosticOption: effectiveOptions.TreatWarningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        stopwatch.Stop();

        var diagnostics = emitResult.Diagnostics
            .Select(diag => new CompilationError
            {
                Code = diag.Id,
                Message = diag.GetMessage(),
                Severity = diag.Severity,
                Line = diag.Location.GetLineSpan().StartLinePosition.Line + 1,
                Column = diag.Location.GetLineSpan().StartLinePosition.Character + 1,
                FilePath = diag.Location.SourceTree?.FilePath
            })
            .ToList();

        return new CompilationResult
        {
            Success = emitResult.Success,
            AssemblyBytes = emitResult.Success ? peStream.ToArray() : null,
            CompilationTime = stopwatch.Elapsed,
            Errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList(),
            Warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList()
        };
    }

    public Task<IReadOnlyList<Diagnostic>> CheckSyntaxAsync(string sourceCode, CancellationToken cancellationToken = default)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode ?? string.Empty);
        var diagnostics = tree.GetDiagnostics(cancellationToken);
        return Task.FromResult<IReadOnlyList<Diagnostic>>(diagnostics.ToList());
    }

    public bool IsValidSyntax(string sourceCode)
    {
        var diagnostics = CSharpSyntaxTree.ParseText(sourceCode ?? string.Empty).GetDiagnostics();
        return !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    }
}

