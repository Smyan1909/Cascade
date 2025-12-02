namespace Cascade.Database.Enums;

/// <summary>
/// Represents the current status of an agent.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// Agent is active and ready for execution.
    /// </summary>
    Active,

    /// <summary>
    /// Agent is inactive and will not be executed.
    /// </summary>
    Inactive,

    /// <summary>
    /// Agent is in draft mode and not yet ready for use.
    /// </summary>
    Draft,

    /// <summary>
    /// Agent has been archived and is no longer in active use.
    /// </summary>
    Archived
}

