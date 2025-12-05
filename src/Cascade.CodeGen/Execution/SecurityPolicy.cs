namespace Cascade.CodeGen.Execution;

public sealed class SecurityPolicy
{
    public bool AllowFileRead { get; set; }
    public bool AllowFileWrite { get; set; }
    public IReadOnlyList<string> AllowedReadPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedWritePaths { get; set; } = Array.Empty<string>();

    public bool AllowNetwork { get; set; }
    public IReadOnlyList<string> AllowedHosts { get; set; } = Array.Empty<string>();

    public bool AllowProcessStart { get; set; }
    public IReadOnlyList<string> AllowedProcesses { get; set; } = Array.Empty<string>();

    public int MaxMemoryMB { get; set; } = 512;
    public int MaxThreads { get; set; } = 10;
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(10);

    public bool AllowReflection { get; set; }
    public bool AllowUnsafeCode { get; set; }

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
        AllowUnsafeCode = true,
        MaxMemoryMB = 2048,
        MaxThreads = 50,
        MaxExecutionTime = TimeSpan.FromHours(1)
    };
}

