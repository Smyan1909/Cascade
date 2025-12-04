namespace Cascade.UIAutomation.Services;

public enum UIAutomationErrorCode
{
    ElementNotFound,
    ElementNotEnabled,
    ElementNotVisible,
    PatternNotSupported,
    ActionFailed,
    Timeout,
    ProcessNotFound,
    WindowNotFound,
    InvalidOperation,
    SessionUnavailable,
    SessionExpired
}

public class UIAutomationException : Exception
{
    public UIAutomationException(string message, UIAutomationErrorCode errorCode, string? elementId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ElementId = elementId;
    }

    public string? ElementId { get; }
    public UIAutomationErrorCode ErrorCode { get; }
}


