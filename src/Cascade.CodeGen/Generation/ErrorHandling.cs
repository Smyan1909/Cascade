namespace Cascade.CodeGen.Generation;

/// <summary>
/// Defines how errors should be handled in a workflow.
/// </summary>
public enum ErrorHandling
{
    StopOnError,
    ContinueOnError,
    RetryThenContinue,
    RetryThenStop
}

