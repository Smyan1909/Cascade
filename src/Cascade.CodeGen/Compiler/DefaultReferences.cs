using System.Reflection;
using System.Text.Json;
using Cascade.UIAutomation.Elements;
using Cascade.Vision.Capture;

namespace Cascade.CodeGen.Compiler;

/// <summary>
/// Provides default assembly references for code compilation.
/// </summary>
public static class DefaultReferences
{
    /// <summary>
    /// Gets the default assemblies that should be referenced for generated scripts.
    /// </summary>
    /// <returns>List of assemblies to reference.</returns>
    public static IReadOnlyList<Assembly> GetDefaultAssemblies()
    {
        var assemblies = new List<Assembly>
        {
            typeof(object).Assembly,                    // System.Private.CoreLib (contains System.Runtime)
            typeof(Console).Assembly,                   // System.Console
            typeof(Task).Assembly,                      // System.Threading.Tasks
            typeof(Enumerable).Assembly,                // System.Linq
            typeof(List<>).Assembly,                    // System.Collections
            typeof(JsonSerializer).Assembly,            // System.Text.Json
            typeof(IUIElement).Assembly,                // Cascade.UIAutomation
            typeof(CaptureResult).Assembly,             // Cascade.Vision
        };
        
        // Add System.Runtime explicitly if not already included
        var systemRuntime = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (systemRuntime != null && !assemblies.Contains(systemRuntime))
        {
            assemblies.Add(systemRuntime);
        }
        
        return assemblies;
    }
}

