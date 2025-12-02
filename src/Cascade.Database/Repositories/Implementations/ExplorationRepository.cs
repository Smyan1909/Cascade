using Cascade.Database.Context;
using Cascade.Database.Entities;
using Cascade.Database.Filters;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Database.Repositories.Implementations;

/// <summary>
/// Implementation of the exploration repository.
/// </summary>
public class ExplorationRepository : IExplorationRepository
{
    private readonly CascadeDbContext _context;

    public ExplorationRepository(CascadeDbContext context)
    {
        _context = context;
    }

    public async Task<ExplorationSession?> GetSessionAsync(Guid id)
    {
        return await _context.ExplorationSessions
            .Include(s => s.Results)
            .Include(s => s.GeneratedAgent)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IReadOnlyList<ExplorationSession>> GetSessionsAsync(ExplorationFilter? filter = null)
    {
        var query = BuildQuery(filter);
        return await query.ToListAsync();
    }

    public async Task<ExplorationSession> CreateSessionAsync(ExplorationSession session)
    {
        if (session.Id == Guid.Empty)
        {
            session.Id = Guid.NewGuid();
        }

        _context.ExplorationSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<ExplorationSession> UpdateSessionAsync(ExplorationSession session)
    {
        _context.ExplorationSessions.Update(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task DeleteSessionAsync(Guid id)
    {
        var session = await _context.ExplorationSessions.FindAsync(id);
        if (session != null)
        {
            _context.ExplorationSessions.Remove(session);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddResultAsync(Guid sessionId, ExplorationResult result)
    {
        if (result.Id == Guid.Empty)
        {
            result.Id = Guid.NewGuid();
        }

        result.SessionId = sessionId;
        _context.ExplorationResults.Add(result);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ExplorationResult>> GetResultsAsync(Guid sessionId)
    {
        return await _context.ExplorationResults
            .Where(r => r.SessionId == sessionId)
            .OrderBy(r => r.CapturedAt)
            .ToListAsync();
    }

    public async Task<int> GetSessionCountAsync(ExplorationFilter? filter = null)
    {
        var query = BuildQuery(filter);
        return await query.CountAsync();
    }

    private IQueryable<ExplorationSession> BuildQuery(ExplorationFilter? filter)
    {
        IQueryable<ExplorationSession> query = _context.ExplorationSessions;

        if (filter == null)
        {
            return query.OrderByDescending(s => s.StartedAt);
        }

        if (!string.IsNullOrEmpty(filter.TargetApplication))
        {
            query = query.Where(s => s.TargetApplication.Contains(filter.TargetApplication));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.Status == filter.Status.Value);
        }

        if (filter.StartedAfter.HasValue)
        {
            query = query.Where(s => s.StartedAt >= filter.StartedAfter.Value);
        }

        if (filter.StartedBefore.HasValue)
        {
            query = query.Where(s => s.StartedAt <= filter.StartedBefore.Value);
        }

        if (filter.HasGeneratedAgent.HasValue)
        {
            query = filter.HasGeneratedAgent.Value
                ? query.Where(s => s.GeneratedAgentId != null)
                : query.Where(s => s.GeneratedAgentId == null);
        }

        // Apply ordering
        query = filter.OrderBy?.ToLower() switch
        {
            "targetapplication" => filter.OrderDescending 
                ? query.OrderByDescending(s => s.TargetApplication) 
                : query.OrderBy(s => s.TargetApplication),
            "status" => filter.OrderDescending 
                ? query.OrderByDescending(s => s.Status) 
                : query.OrderBy(s => s.Status),
            "completedat" => filter.OrderDescending 
                ? query.OrderByDescending(s => s.CompletedAt) 
                : query.OrderBy(s => s.CompletedAt),
            _ => filter.OrderDescending 
                ? query.OrderByDescending(s => s.StartedAt) 
                : query.OrderBy(s => s.StartedAt)
        };

        // Apply pagination
        if (filter.Skip > 0)
        {
            query = query.Skip(filter.Skip);
        }

        if (filter.Take > 0)
        {
            query = query.Take(filter.Take);
        }

        return query;
    }
}

