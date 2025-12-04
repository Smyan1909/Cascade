using Cascade.Database.Context;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Core;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Database.Repositories.Implementations;

/// <summary>
/// Entity Framework implementation for session persistence.
/// </summary>
public class SessionRepository : ISessionRepository
{
    private readonly CascadeDbContext _context;

    public SessionRepository(CascadeDbContext context)
    {
        _context = context;
    }

    public async Task<AutomationSession> CreateAsync(AutomationSession session)
    {
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            throw new ArgumentException("SessionId is required", nameof(session));
        }

        if (session.Id == Guid.Empty)
        {
            session.Id = Guid.NewGuid();
        }

        session.Profile ??= VirtualDesktopProfile.Default;

        _context.AutomationSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<AutomationSession?> GetBySessionIdAsync(string sessionId, bool includeEvents = false)
    {
        IQueryable<AutomationSession> query = _context.AutomationSessions
            .Include(s => s.Agent)
            .Include(s => s.ExecutionRecord);

        if (includeEvents)
        {
            query = query.Include(s => s.Events);
        }

        return await query.FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task<IReadOnlyList<AutomationSession>> GetByAgentIdAsync(Guid agentId, bool includeReleased = false)
    {
        var query = _context.AutomationSessions
            .Where(s => s.AgentId == agentId);

        if (!includeReleased)
        {
            query = query.Where(s => s.State != SessionState.Released);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateStateAsync(string sessionId, SessionState newState)
    {
        var session = await _context.AutomationSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null)
        {
            return;
        }

        session.State = newState;

        if (newState == SessionState.Released && session.ReleasedAt == null)
        {
            session.ReleasedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task AddEventAsync(SessionEvent sessionEvent)
    {
        if (sessionEvent.Id == Guid.Empty)
        {
            sessionEvent.Id = Guid.NewGuid();
        }

        if (sessionEvent.OccurredAt == default)
        {
            sessionEvent.OccurredAt = DateTime.UtcNow;
        }

        _context.SessionEvents.Add(sessionEvent);
        await _context.SaveChangesAsync();
    }

    public async Task ReleaseAsync(string sessionId, string reason)
    {
        var session = await _context.AutomationSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null)
        {
            return;
        }

        session.State = SessionState.Released;
        session.ReleasedAt ??= DateTime.UtcNow;

        var releaseEvent = new SessionEvent
        {
            Id = Guid.NewGuid(),
            AutomationSessionId = session.Id,
            EventType = "Released",
            Payload = reason,
            OccurredAt = DateTime.UtcNow
        };

        _context.SessionEvents.Add(releaseEvent);

        await _context.SaveChangesAsync();
    }
}


