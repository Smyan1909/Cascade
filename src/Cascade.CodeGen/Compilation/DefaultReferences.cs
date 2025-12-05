using Cascade.CodeGen.Execution;
using Cascade.Vision.Capture;
using Microsoft.CodeAnalysis;

namespace Cascade.CodeGen.Compilation;

public static class DefaultReferences
{
    public static IReadOnlyList<MetadataReference> GetReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Task).Assembly,
            typeof(Cascade.UIAutomation.Discovery.SearchCriteria).Assembly,
            typeof(Cascade.CodeGen.Generation.ElementLocator).Assembly,
            typeof(ActionRuntimeRequest).Assembly,
            typeof(IScreenCapture).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly
        };

        return assemblies
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToList();
    }
}

