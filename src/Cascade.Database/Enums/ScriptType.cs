namespace Cascade.Database.Enums;

/// <summary>
/// Represents the type of a script.
/// </summary>
public enum ScriptType
{
    /// <summary>
    /// A single UI action script.
    /// </summary>
    Action,

    /// <summary>
    /// A workflow combining multiple actions.
    /// </summary>
    Workflow,

    /// <summary>
    /// A full agent script with decision-making logic.
    /// </summary>
    Agent,

    /// <summary>
    /// A test script for validation purposes.
    /// </summary>
    Test,

    /// <summary>
    /// A utility script for helper functions.
    /// </summary>
    Utility
}

