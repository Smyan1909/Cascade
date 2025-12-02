using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Exceptions;

/// <summary>
/// Exception thrown when a UI Automation operation fails.
/// </summary>
public class UIAutomationException : Exception
{
    /// <summary>
    /// Gets the identifier of the element involved in the failed operation, if any.
    /// </summary>
    public string? ElementId { get; }

    /// <summary>
    /// Gets the error code indicating the type of failure.
    /// </summary>
    public UIAutomationErrorCode ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAutomationException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code indicating the type of failure.</param>
    /// <param name="message">The error message.</param>
    public UIAutomationException(UIAutomationErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAutomationException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code indicating the type of failure.</param>
    /// <param name="message">The error message.</param>
    /// <param name="elementId">The identifier of the element involved in the failure.</param>
    public UIAutomationException(UIAutomationErrorCode errorCode, string message, string? elementId)
        : base(message)
    {
        ErrorCode = errorCode;
        ElementId = elementId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAutomationException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code indicating the type of failure.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public UIAutomationException(UIAutomationErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAutomationException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code indicating the type of failure.</param>
    /// <param name="message">The error message.</param>
    /// <param name="elementId">The identifier of the element involved in the failure.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public UIAutomationException(UIAutomationErrorCode errorCode, string message, string? elementId, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ElementId = elementId;
    }

    /// <summary>
    /// Creates an exception for when an element is not found.
    /// </summary>
    public static UIAutomationException ElementNotFound(string criteria)
        => new(UIAutomationErrorCode.ElementNotFound, $"Element not found matching criteria: {criteria}");

    /// <summary>
    /// Creates an exception for when an element is not enabled.
    /// </summary>
    public static UIAutomationException ElementNotEnabled(string? elementId)
        => new(UIAutomationErrorCode.ElementNotEnabled, "Element is not enabled", elementId);

    /// <summary>
    /// Creates an exception for when an element is not visible.
    /// </summary>
    public static UIAutomationException ElementNotVisible(string? elementId)
        => new(UIAutomationErrorCode.ElementNotVisible, "Element is not visible or is offscreen", elementId);

    /// <summary>
    /// Creates an exception for when a pattern is not supported.
    /// </summary>
    public static UIAutomationException PatternNotSupported(string patternName, string? elementId)
        => new(UIAutomationErrorCode.PatternNotSupported, $"Pattern '{patternName}' is not supported by this element", elementId);

    /// <summary>
    /// Creates an exception for when an action fails.
    /// </summary>
    public static UIAutomationException ActionFailed(string action, string? elementId, Exception? innerException = null)
        => innerException != null
            ? new(UIAutomationErrorCode.ActionFailed, $"Action '{action}' failed", elementId, innerException)
            : new(UIAutomationErrorCode.ActionFailed, $"Action '{action}' failed", elementId);

    /// <summary>
    /// Creates an exception for when an operation times out.
    /// </summary>
    public static UIAutomationException Timeout(string operation, TimeSpan timeout)
        => new(UIAutomationErrorCode.Timeout, $"Operation '{operation}' timed out after {timeout.TotalSeconds:F1} seconds");

    /// <summary>
    /// Creates an exception for when a process is not found.
    /// </summary>
    public static UIAutomationException ProcessNotFound(string processIdentifier)
        => new(UIAutomationErrorCode.ProcessNotFound, $"Process not found: {processIdentifier}");

    /// <summary>
    /// Creates an exception for when a window is not found.
    /// </summary>
    public static UIAutomationException WindowNotFound(string windowIdentifier)
        => new(UIAutomationErrorCode.WindowNotFound, $"Window not found: {windowIdentifier}");

    /// <summary>
    /// Creates an exception for an invalid operation.
    /// </summary>
    public static UIAutomationException InvalidOperation(string message)
        => new(UIAutomationErrorCode.InvalidOperation, message);

    /// <summary>
    /// Creates an exception for a stale element reference.
    /// </summary>
    public static UIAutomationException StaleElement(string? elementId)
        => new(UIAutomationErrorCode.StaleElement, "Element reference is stale and no longer valid", elementId);

    /// <summary>
    /// Creates an exception for a COM error.
    /// </summary>
    public static UIAutomationException ComError(string message, Exception innerException)
        => new(UIAutomationErrorCode.ComError, message, innerException);
}

