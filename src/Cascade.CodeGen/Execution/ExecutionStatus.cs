namespace Cascade.CodeGen.Execution;

/// <summary>
/// Status of script execution.
/// </summary>
public enum ExecutionStatus
{
    Completed,
    Failed,
    Timeout,
    Cancelled,
    SecurityViolation
}

