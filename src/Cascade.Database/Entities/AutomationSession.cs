using Cascade.Core;
using Cascade.Database.Enums;

namespace Cascade.Database.Entities;

/// <summary>
/// Represents a hidden desktop session managed by the Session Host.
/// </summary>
public class AutomationSession
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public Guid AgentId { get; set; }
    public Guid? ExecutionRecordId { get; set; }
    public string RunId { get; set; } = string.Empty;
    public VirtualDesktopProfile Profile { get; set; } = new();
    public SessionState State { get; set; } = SessionState.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    public Agent Agent { get; set; } = null!;
    public ExecutionRecord? ExecutionRecord { get; set; }
    public ICollection<SessionEvent> Events { get; set; } = new List<SessionEvent>();
}



