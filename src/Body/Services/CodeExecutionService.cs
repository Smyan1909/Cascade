using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Text;
using System.Text.Json;
using Cascade.Proto;
using Grpc.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Body.Services;

public sealed class CodeExecutionService : Proto.CodeExecutionService.CodeExecutionServiceBase
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    public override async Task<CodeExecutionResult> ExecuteCode(CodeExecutionRequest request, ServerCallContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var language = (request.Language ?? string.Empty).Trim().ToLowerInvariant();
            if (language != "csharp" && language != "cs" && language != "c#")
            {
                return new CodeExecutionResult
                {
                    Success = false,
                    Error = $"Unsupported language '{request.Language}'. Body currently supports C# only.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            var sources = request.Files
                .Where(f => !string.IsNullOrWhiteSpace(f.Content))
                .Select(f => f.Content)
                .ToList();

            if (sources.Count == 0)
            {
                return new CodeExecutionResult
                {
                    Success = false,
                    Error = "No inline code files provided (request.files is empty).",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Basic heuristic guardrails (NOT a sandbox; approvals are enforced Brain-side).
            // These are meant to avoid accidental obviously-dangerous snippets.
            var combined = string.Join("\n", sources);
            var deniedMarkers = new[]
            {
                "System.IO",
                "System.Net",
                "System.Diagnostics.Process",
                "ProcessStartInfo",
            };
            if (deniedMarkers.Any(m => combined.Contains(m, StringComparison.OrdinalIgnoreCase)))
            {
                return new CodeExecutionResult
                {
                    Success = false,
                    Error = "Code contains denied namespace/API markers (System.IO/System.Net/Process). Use approvals+connector tools instead.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            var compilation = BuildCompilation(sources);
            await using var peStream = new MemoryStream();
            var emitResult = compilation.Emit(peStream);
            if (!emitResult.Success)
            {
                var diag = string.Join(
                    "\n",
                    emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
                        .Select(d => d.ToString())
                );
                return new CodeExecutionResult
                {
                    Success = false,
                    Error = $"Compilation failed:\n{diag}",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            peStream.Position = 0;
            var assembly = AssemblyLoadContext.Default.LoadFromStream(peStream);

            var entry = FindEntrypoint(assembly);
            if (entry is null)
            {
                return new CodeExecutionResult
                {
                    Success = false,
                    Error = "No entrypoint found. Provide a public static method Run(string inputsJson) that returns string.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            var inputsJson = JsonSerializer.Serialize(request.Inputs.ToDictionary(kv => kv.Key, kv => kv.Value));

            var timeout = DefaultTimeout;
            var remaining = context.Deadline - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero && remaining < timeout)
            {
                timeout = remaining;
            }
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(timeout);

            // COM automation (Excel/Office/etc.) requires STA threads. If the request declares a COM capability,
            // run the entrypoint on an STA thread to avoid COM initialization/apartment issues.
            var requiresSta = request.Capabilities.Any(c =>
                string.Equals(c.Type, "com", StringComparison.OrdinalIgnoreCase));

            var output = await InvokeWithCapturedConsole(entry, inputsJson, cts.Token, requiresSta).ConfigureAwait(false);

            return new CodeExecutionResult
            {
                Success = true,
                Output = output,
                Error = "",
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            return new CodeExecutionResult
            {
                Success = false,
                Output = "",
                Error = "Execution timed out or was cancelled.",
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            return new CodeExecutionResult
            {
                Success = false,
                Output = "",
                Error = ex.ToString(),
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    private static CSharpCompilation BuildCompilation(List<string> sources)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest))).ToList();
        var references = BuildMetadataReferences();

        return CSharpCompilation.Create(
            assemblyName: $"Cascade_CodeExec_{Guid.NewGuid():N}",
            syntaxTrees: trees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release)
        );
    }

    private static List<MetadataReference> BuildMetadataReferences()
    {
        // Use the Trusted Platform Assemblies list and allow a conservative subset.
        // This is not a security sandbox; it's mostly to avoid pulling in random assemblies.
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? string.Empty;
        var paths = tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        static bool Allowed(string fileName)
        {
            // Examples: System.Runtime.dll, System.Private.CoreLib.dll, netstandard.dll, Microsoft.CSharp.dll
            return fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                   || fileName.Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase)
                   || fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                   || fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                   || fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase);
        }

        var refs = new List<MetadataReference>();
        foreach (var p in paths)
        {
            var name = Path.GetFileName(p);
            if (!Allowed(name))
            {
                continue;
            }
            refs.Add(MetadataReference.CreateFromFile(p));
        }

        return refs;
    }

    private static MethodInfo? FindEntrypoint(Assembly assembly)
    {
        // Preferred: public static string Run(string inputsJson) on type named SkillEntrypoint.
        foreach (var t in assembly.GetTypes())
        {
            if (!t.IsClass) continue;
            if (!string.Equals(t.Name, "SkillEntrypoint", StringComparison.OrdinalIgnoreCase)) continue;
            var m = t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            if (IsCompatibleEntrypoint(m)) return m;
        }

        // Fallback: first public static string Run(string) anywhere.
        foreach (var t in assembly.GetTypes())
        {
            if (!t.IsClass) continue;
            var m = t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            if (IsCompatibleEntrypoint(m)) return m;
        }

        return null;
    }

    private static bool IsCompatibleEntrypoint(MethodInfo? method)
    {
        if (method is null) return false;
        if (method.ReturnType != typeof(string)) return false;
        var ps = method.GetParameters();
        return ps.Length == 1 && ps[0].ParameterType == typeof(string);
    }

    private static async Task<string> InvokeWithCapturedConsole(MethodInfo entry, string inputsJson, CancellationToken ct, bool requiresSta)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var sb = new StringBuilder();
        await using var outWriter = new StringWriter(sb);
        await using var errWriter = new StringWriter(sb);

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            Task<string> invokeTask;
            if (!requiresSta)
            {
                invokeTask = Task.Run(() => (string?)entry.Invoke(null, new object?[] { inputsJson }) ?? string.Empty);
            }
            else
            {
                invokeTask = InvokeOnStaThread(entry, inputsJson, ct);
            }

            var completed = await Task.WhenAny(invokeTask, Task.Delay(Timeout.InfiniteTimeSpan, ct)).ConfigureAwait(false);
            if (completed != invokeTask)
            {
                throw new OperationCanceledException(ct);
            }
            var result = await invokeTask.ConfigureAwait(false);

            await outWriter.FlushAsync().ConfigureAwait(false);
            await errWriter.FlushAsync().ConfigureAwait(false);

            var console = sb.ToString();
            if (!string.IsNullOrWhiteSpace(console))
            {
                return $"{console}\n\n[return]\n{result}";
            }
            return result;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static Task<string> InvokeOnStaThread(MethodInfo entry, string inputsJson, CancellationToken ct)
    {
        // Note: this is not a security sandbox. STA is used to support COM automation (Office apps).
        // Cancellation cannot abort managed code safely; we rely on the server-side timeout and will return a cancellation error.
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var result = (string?)entry.Invoke(null, new object?[] { inputsJson }) ?? string.Empty;
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Cascade_CodeExec_STA"
        };

        try
        {
            thread.SetApartmentState(ApartmentState.STA);
        }
        catch
        {
            // If we can't set STA (shouldn't happen on Windows), fall back to MTA invocation.
        }

        thread.Start();

        // Best-effort cancellation: if ct fires, return cancellation (thread may continue in background).
        ct.Register(() => tcs.TrySetCanceled(ct));
        return tcs.Task;
    }
}


