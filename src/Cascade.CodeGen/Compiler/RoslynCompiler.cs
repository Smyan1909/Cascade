using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;

namespace Cascade.CodeGen.Compiler;

/// <summary>
/// Roslyn-based implementation of the script compiler.
/// </summary>
public class RoslynCompiler : IScriptCompiler
{
    /// <inheritdoc />
    public async Task<CompilationResult> CompileAsync(string sourceCode, CompilationOptions? options = null)
    {
        var startTime = DateTime.UtcNow;
        options ??= new CompilationOptions();

        try
        {
            var parseOptions = new CSharpParseOptions(
                    options.LanguageVersion,
                    DocumentationMode.None);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);

            var compilation = CreateCompilation(syntaxTree, options);
            var assemblyName = options.AssemblyName ?? $"Generated_{Guid.NewGuid():N}";

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            var result = new CompilationResult
            {
                CompilationTime = DateTime.UtcNow - startTime
            };

            if (emitResult.Success)
            {
                ms.Position = 0;
                result.AssemblyBytes = ms.ToArray();
                result.Assembly = Assembly.Load(result.AssemblyBytes);
                result.Success = true;
            }
            else
            {
                result.Success = false;
                result.Errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(ConvertDiagnosticToError)
                    .ToList();
                result.Warnings = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Warning)
                    .Select(ConvertDiagnosticToWarning)
                    .ToList();
            }

            return result;
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new List<CompilationError>
                {
                    new()
                    {
                        Code = "COMPILE_ERROR",
                        Message = $"Compilation failed: {ex.Message}",
                        Severity = CompilationErrorSeverity.Error
                    }
                },
                CompilationTime = DateTime.UtcNow - startTime
            };
        }
    }

    /// <inheritdoc />
    public async Task<CompilationResult> CompileFilesAsync(IEnumerable<string> filePaths, CompilationOptions? options = null)
    {
        options ??= new CompilationOptions();
        var syntaxTrees = new List<SyntaxTree>();

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Source file not found: {filePath}", filePath);

            var sourceCode = await File.ReadAllTextAsync(filePath);
            var parseOptions = new CSharpParseOptions(
                    options.LanguageVersion,
                    DocumentationMode.None);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath, options: parseOptions);

            syntaxTrees.Add(syntaxTree);
        }

        var startTime = DateTime.UtcNow;
        var compilation = CreateCompilation(syntaxTrees, options);
        var assemblyName = options.AssemblyName ?? $"Generated_{Guid.NewGuid():N}";

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        var result = new CompilationResult
        {
            CompilationTime = DateTime.UtcNow - startTime
        };

        if (emitResult.Success)
        {
            ms.Position = 0;
            result.AssemblyBytes = ms.ToArray();
            result.Assembly = Assembly.Load(result.AssemblyBytes);
            result.Success = true;
        }
        else
        {
            result.Success = false;
            result.Errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(ConvertDiagnosticToError)
                .ToList();
            result.Warnings = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(ConvertDiagnosticToWarning)
                .ToList();
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ScriptResult<T>> EvaluateAsync<T>(string expression, ScriptGlobals? globals = null)
    {
        try
        {
            var scriptOptions = ScriptOptions.Default
                .WithReferences(DefaultReferences.GetDefaultAssemblies())
                .WithImports("System", "System.Linq", "System.Collections.Generic", "System.Threading.Tasks");

            Script<T> script;
            if (globals != null && globals.Variables.Any())
            {
                var globalsType = CreateGlobalsType(globals.Variables);
                script = CSharpScript.Create<T>(expression, scriptOptions, globalsType: globalsType);
            }
            else
            {
                script = CSharpScript.Create<T>(expression, scriptOptions);
            }

            var result = await script.RunAsync(globals?.Variables);
            return new ScriptResult<T>
            {
                Success = true,
                ReturnValue = result.ReturnValue
            };
        }
        catch (Exception ex)
        {
            return new ScriptResult<T>
            {
                Success = false,
                Exception = ex
            };
        }
    }

    /// <inheritdoc />
    public async Task<ScriptResult> EvaluateAsync(string script, ScriptGlobals? globals = null)
    {
        try
        {
            var scriptOptions = ScriptOptions.Default
                .WithReferences(DefaultReferences.GetDefaultAssemblies())
                .WithImports("System", "System.Linq", "System.Collections.Generic", "System.Threading.Tasks");

            Script<object> csharpScript;
            if (globals != null && globals.Variables.Any())
            {
                var globalsType = CreateGlobalsType(globals.Variables);
                csharpScript = CSharpScript.Create<object>(script, scriptOptions, globalsType: globalsType);
            }
            else
            {
                csharpScript = CSharpScript.Create<object>(script, scriptOptions);
            }

            var result = await csharpScript.RunAsync(globals?.Variables);
            return new ScriptResult
            {
                Success = true,
                ReturnValue = result.ReturnValue
            };
        }
        catch (Exception ex)
        {
            return new ScriptResult
            {
                Success = false,
                Exception = ex
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Diagnostic>> CheckSyntaxAsync(string sourceCode)
    {
        return await Task.Run(() =>
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            return syntaxTree.GetDiagnostics().ToList();
        });
    }

    /// <inheritdoc />
    public bool IsValidSyntax(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        return !syntaxTree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    private CSharpCompilation CreateCompilation(SyntaxTree syntaxTree, CompilationOptions options)
    {
        return CreateCompilation(new[] { syntaxTree }, options);
    }

    private CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> syntaxTrees, CompilationOptions options)
    {
        var assemblyName = options.AssemblyName ?? $"Generated_{Guid.NewGuid():N}";
        var references = GetReferences(options);

        var compilationOptions = new CSharpCompilationOptions(options.OutputKind)
            .WithOptimizationLevel(options.OptimizationLevel)
            .WithNullableContextOptions(options.NullableContextOptions)
            .WithGeneralDiagnosticOption(options.TreatWarningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default);

        if (options.SuppressedWarnings.Any())
        {
            var reportDiagnostic = options.SuppressedWarnings
                .ToDictionary(w => w, _ => ReportDiagnostic.Suppress);
            compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(reportDiagnostic);
        }

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            compilationOptions);
    }

    private IEnumerable<MetadataReference> GetReferences(CompilationOptions options)
    {
        var references = new List<MetadataReference>();

        if (options.IncludeDefaultReferences)
        {
            foreach (var assembly in DefaultReferences.GetDefaultAssemblies())
            {
                try
                {
                    if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                    {
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                }
                catch
                {
                    // Skip assemblies we can't reference
                    continue;
                }
            }
        }

        foreach (var assembly in options.AssemblyReferences)
        {
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        foreach (var filePath in options.References)
        {
            if (File.Exists(filePath))
            {
                references.Add(MetadataReference.CreateFromFile(filePath));
            }
        }

        return references;
    }

    private CompilationError ConvertDiagnosticToError(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var lineSpan = location.GetLineSpan();

        return new CompilationError
        {
            Code = diagnostic.Id,
            Message = diagnostic.GetMessage(),
            Line = lineSpan.StartLinePosition.Line + 1, // 1-based
            Column = lineSpan.StartLinePosition.Character + 1, // 1-based
            FilePath = location.SourceTree?.FilePath,
            Severity = diagnostic.Severity == DiagnosticSeverity.Error
                ? CompilationErrorSeverity.Error
                : CompilationErrorSeverity.Warning
        };
    }

    private CompilationWarning ConvertDiagnosticToWarning(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var lineSpan = location.GetLineSpan();

        return new CompilationWarning
        {
            Code = diagnostic.Id,
            Message = diagnostic.GetMessage(),
            Line = lineSpan.StartLinePosition.Line + 1, // 1-based
            Column = lineSpan.StartLinePosition.Character + 1, // 1-based
            FilePath = location.SourceTree?.FilePath
        };
    }

    private Type CreateGlobalsType(Dictionary<string, object> variables)
    {
        // For simple cases, we can use an anonymous type or create a dynamic approach
        // For now, we'll use a simpler approach with ScriptState
        // This is a simplified implementation - in practice, you might want to generate a type dynamically
        return typeof(object); // Placeholder - CSharpScript will handle this internally
    }
}

