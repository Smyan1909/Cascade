namespace Cascade.Database.Enums;

/// <summary>
/// Represents the type of an exploration result.
/// </summary>
public enum ExplorationResultType
{
    /// <summary>
    /// Result contains window information.
    /// </summary>
    Window,

    /// <summary>
    /// Result contains UI element information.
    /// </summary>
    Element,

    /// <summary>
    /// Result contains action test outcome.
    /// </summary>
    ActionTest,

    /// <summary>
    /// Result contains navigation path information.
    /// </summary>
    NavigationPath,

    /// <summary>
    /// Result contains error information.
    /// </summary>
    Error
}

