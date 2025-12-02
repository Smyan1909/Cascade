namespace Cascade.Database.Enums;

/// <summary>
/// Represents the status of an exploration session.
/// </summary>
public enum ExplorationStatus
{
    /// <summary>
    /// Exploration is pending and has not started.
    /// </summary>
    Pending,

    /// <summary>
    /// Exploration is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Exploration completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Exploration failed due to an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Exploration was cancelled by the user.
    /// </summary>
    Cancelled
}

