namespace Cascade.CodeGen.Generation;

/// <summary>
/// Defines an agent class with capabilities and actions.
/// </summary>
public class AgentDefinition
{
    /// <summary>
    /// Name of the agent class.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the agent does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of capabilities this agent provides.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; set; } = new List<string>();

    /// <summary>
    /// List of actions this agent can perform.
    /// </summary>
    public IReadOnlyList<ActionDefinition> Actions { get; set; } = new List<ActionDefinition>();

    /// <summary>
    /// Target application name or identifier.
    /// </summary>
    public string TargetApplication { get; set; } = string.Empty;
}

