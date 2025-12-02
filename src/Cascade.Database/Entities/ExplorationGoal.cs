namespace Cascade.Database.Entities;

/// <summary>
/// Represents an exploration goal within an exploration session.
/// This is a value object stored as JSON within ExplorationSession.
/// </summary>
public class ExplorationGoal
{
    /// <summary>
    /// Unique identifier for the goal.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Description of what needs to be explored/achieved.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// UI elements that need to be found for this goal.
    /// </summary>
    public List<string> TargetElements { get; set; } = new();

    /// <summary>
    /// Actions that need to be performed/tested.
    /// </summary>
    public List<string> RequiredActions { get; set; } = new();

    /// <summary>
    /// Criteria to determine if the goal was achieved.
    /// </summary>
    public string SuccessCriteria { get; set; } = string.Empty;

    /// <summary>
    /// IDs of other goals that must be completed first.
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
}

