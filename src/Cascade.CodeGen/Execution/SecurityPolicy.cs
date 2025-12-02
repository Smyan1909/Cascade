namespace Cascade.CodeGen.Execution;

/// <summary>
/// Defines security restrictions for script execution.
/// </summary>
public class SecurityPolicy
{
    // File system
    public bool AllowFileRead { get; set; } = false;
    public bool AllowFileWrite { get; set; } = false;
    public IReadOnlyList<string> AllowedReadPaths { get; set; } = new List<string>();
    public IReadOnlyList<string> AllowedWritePaths { get; set; } = new List<string>();

    // Network
    public bool AllowNetwork { get; set; } = false;
    public IReadOnlyList<string> AllowedHosts { get; set; } = new List<string>();

    // Process
    public bool AllowProcessStart { get; set; } = false;
    public IReadOnlyList<string> AllowedProcesses { get; set; } = new List<string>();

    // Resources
    public int MaxMemoryMB { get; set; } = 512;
    public int MaxThreads { get; set; } = 10;
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(10);

    // Reflection
    public bool AllowReflection { get; set; } = false;
    public bool AllowUnsafeCode { get; set; } = false;

    // Presets
    public static SecurityPolicy Default { get; } = new();

    public static SecurityPolicy Strict { get; } = new()
    {
        AllowFileRead = false,
        AllowFileWrite = false,
        AllowNetwork = false,
        AllowProcessStart = false,
        AllowReflection = false,
        MaxMemoryMB = 256,
        MaxThreads = 5,
        MaxExecutionTime = TimeSpan.FromMinutes(1)
    };

    public static SecurityPolicy Permissive { get; } = new()
    {
        AllowFileRead = true,
        AllowFileWrite = true,
        AllowNetwork = true,
        AllowProcessStart = true,
        AllowReflection = true,
        MaxMemoryMB = 2048,
        MaxThreads = 50,
        MaxExecutionTime = TimeSpan.FromHours(1)
    };
}

