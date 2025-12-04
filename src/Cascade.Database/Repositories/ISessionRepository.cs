using Cascade.Database.Entities;
using Cascade.Database.Enums;

namespace Cascade.Database.Repositories;

/// <summary>
/// Repository abstraction for managing automation sessions and events.
/// </summary>
public interface ISessionRepository
{
    Task<AutomationSession> CreateAsync(AutomationSession session);
    Task<AutomationSession?> GetBySessionIdAsync(string sessionId, bool includeEvents = false);
    Task<IReadOnlyList<AutomationSession>> GetByAgentIdAsync(Guid agentId, bool includeReleased = false);
    Task UpdateStateAsync(string sessionId, SessionState newState);
    Task AddEventAsync(SessionEvent sessionEvent);
    Task ReleaseAsync(string sessionId, string reason);
}



