namespace Cascade.CodeGen.Execution;

/// <summary>
/// Result of script execution.
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Whether execution succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Return value from the script (if any).
    /// </summary>
    public object? ReturnValue { get; set; }

    /// <summary>
    /// Exception that occurred during execution (if any).
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Time taken to execute the script.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Unique identifier for this execution.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Log messages produced during execution.
    /// </summary>
    public IReadOnlyList<string> Logs { get; set; } = new List<string>();

    /// <summary>
    /// Status of the execution.
    /// </summary>
    public ExecutionStatus Status { get; set; }
}

/// <summary>
/// Typed result of script execution.
/// </summary>
/// <typeparam name="T">Type of the return value.</typeparam>
public class ExecutionResult<T> : ExecutionResult
{
    /// <summary>
    /// Typed return value from the script.
    /// </summary>
    public new T? ReturnValue { get; set; }
}

