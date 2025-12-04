namespace Cascade.Database.Entities;

/// <summary>
/// Represents a lifecycle event for an automation session.
/// </summary>
public class SessionEvent
{
    public Guid Id { get; set; }
    public Guid AutomationSessionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public AutomationSession Session { get; set; } = null!;
}


