namespace Cascade.UIAutomation.Enums;

/// <summary>
/// Error codes for UI Automation operations.
/// </summary>
public enum UIAutomationErrorCode
{
    /// <summary>
    /// The specified element was not found.
    /// </summary>
    ElementNotFound,

    /// <summary>
    /// The element is not enabled and cannot receive input.
    /// </summary>
    ElementNotEnabled,

    /// <summary>
    /// The element is not visible or is offscreen.
    /// </summary>
    ElementNotVisible,

    /// <summary>
    /// The requested pattern is not supported by the element.
    /// </summary>
    PatternNotSupported,

    /// <summary>
    /// The action could not be performed on the element.
    /// </summary>
    ActionFailed,

    /// <summary>
    /// The operation timed out waiting for an element or condition.
    /// </summary>
    Timeout,

    /// <summary>
    /// The specified process was not found.
    /// </summary>
    ProcessNotFound,

    /// <summary>
    /// The specified window was not found.
    /// </summary>
    WindowNotFound,

    /// <summary>
    /// The operation is not valid in the current state.
    /// </summary>
    InvalidOperation,

    /// <summary>
    /// The element reference is stale and no longer valid.
    /// </summary>
    StaleElement,

    /// <summary>
    /// An unexpected COM error occurred.
    /// </summary>
    ComError
}

