namespace Cascade.CodeGen.Generation;

/// <summary>
/// Types of UI automation actions that can be performed.
/// </summary>
public enum ActionType
{
    Click,
    DoubleClick,
    RightClick,
    Type,
    SetValue,
    Select,
    Check,
    Uncheck,
    Expand,
    Collapse,
    Scroll,
    DragDrop,
    Invoke,
    Focus,
    WaitForElement,
    WaitForText,
    CaptureScreenshot,
    RunOcr,
    Custom
}

