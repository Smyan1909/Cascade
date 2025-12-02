namespace Cascade.UIAutomation.Enums;

/// <summary>
/// The amount to scroll in a scroll operation.
/// </summary>
public enum ScrollAmount
{
    /// <summary>
    /// Scroll by a large decrement (page up/left).
    /// </summary>
    LargeDecrement = 0,

    /// <summary>
    /// Scroll by a small decrement (line up/left).
    /// </summary>
    SmallDecrement = 1,

    /// <summary>
    /// Do not scroll.
    /// </summary>
    NoAmount = 2,

    /// <summary>
    /// Scroll by a large increment (page down/right).
    /// </summary>
    LargeIncrement = 3,

    /// <summary>
    /// Scroll by a small increment (line down/right).
    /// </summary>
    SmallIncrement = 4
}

