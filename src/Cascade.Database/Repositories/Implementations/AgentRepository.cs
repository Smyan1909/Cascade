using Cascade.Database.Context;
using Cascade.Database.Entities;
using Cascade.Database.Filters;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Database.Repositories.Implementations;

/// <summary>
/// Implementation of the agent repository.
/// </summary>
public class AgentRepository : IAgentRepository
{
    private readonly CascadeDbContext _context;

    public AgentRepository(CascadeDbContext context)
    {
        _context = context;
    }

    public async Task<Agent?> GetByIdAsync(Guid id)
    {
        return await _context.Agents
            .Include(a => a.Scripts)
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Agent?> GetByNameAsync(string name)
    {
        return await _context.Agents
            .Include(a => a.Scripts)
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Name == name);
    }

    public async Task<IReadOnlyList<Agent>> GetAllAsync(AgentFilter? filter = null)
    {
        var query = BuildQuery(filter);
        return await query.ToListAsync();
    }

    public async Task<Agent> CreateAsync(Agent agent)
    {
        if (agent.Id == Guid.Empty)
        {
            agent.Id = Guid.NewGuid();
        }

        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();
        return agent;
    }

    public async Task<Agent> UpdateAsync(Agent agent)
    {
        _context.Agents.Update(agent);
        await _context.SaveChangesAsync();
        return agent;
    }

    public async Task DeleteAsync(Guid id)
    {
        var agent = await _context.Agents.FindAsync(id);
        if (agent != null)
        {
            _context.Agents.Remove(agent);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<AgentVersion> CreateVersionAsync(Guid agentId, string? notes = null)
    {
        var agent = await _context.Agents
            .Include(a => a.Scripts)
            .FirstOrDefaultAsync(a => a.Id == agentId);

        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {agentId} not found.");
        }

        // Parse current version and increment
        var currentVersion = agent.ActiveVersion;
        var nextVersion = IncrementVersion(currentVersion);

        // Set all existing versions to inactive
        var existingVersions = await _context.AgentVersions
            .Where(v => v.AgentId == agentId)
            .ToListAsync();

        foreach (var v in existingVersions)
        {
            v.IsActive = false;
        }

        // Create new version snapshot
        var version = new AgentVersion
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Version = nextVersion,
            Notes = notes,
            IsActive = true,
            InstructionListSnapshot = agent.InstructionList,
            CapabilitiesSnapshot = new List<string>(agent.Capabilities),
            ScriptIdsSnapshot = agent.Scripts.Select(s => s.Id).ToList(),
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentVersions.Add(version);

        // Update agent's active version
        agent.ActiveVersion = nextVersion;

        await _context.SaveChangesAsync();
        return version;
    }

    public async Task<IReadOnlyList<AgentVersion>> GetVersionsAsync(Guid agentId)
    {
        return await _context.AgentVersions
            .Where(v => v.AgentId == agentId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task SetActiveVersionAsync(Guid agentId, string version)
    {
        var versions = await _context.AgentVersions
            .Where(v => v.AgentId == agentId)
            .ToListAsync();

        var targetVersion = versions.FirstOrDefault(v => v.Version == version);
        if (targetVersion == null)
        {
            throw new InvalidOperationException($"Version {version} not found for agent {agentId}.");
        }

        foreach (var v in versions)
        {
            v.IsActive = v.Version == version;
        }

        var agent = await _context.Agents.FindAsync(agentId);
        if (agent != null)
        {
            agent.ActiveVersion = version;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCountAsync(AgentFilter? filter = null)
    {
        var query = BuildQuery(filter);
        return await query.CountAsync();
    }

    private IQueryable<Agent> BuildQuery(AgentFilter? filter)
    {
        IQueryable<Agent> query = _context.Agents;

        if (filter == null)
        {
            return query.OrderByDescending(a => a.CreatedAt);
        }

        if (!string.IsNullOrEmpty(filter.TargetApplication))
        {
            query = query.Where(a => a.TargetApplication.Contains(filter.TargetApplication));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(a => a.Status == filter.Status.Value);
        }

        if (!string.IsNullOrEmpty(filter.Name))
        {
            query = query.Where(a => a.Name.Contains(filter.Name));
        }

        if (filter.CreatedAfter.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= filter.CreatedAfter.Value);
        }

        if (filter.CreatedBefore.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= filter.CreatedBefore.Value);
        }

        // Apply ordering
        query = filter.OrderBy?.ToLower() switch
        {
            "name" => filter.OrderDescending 
                ? query.OrderByDescending(a => a.Name) 
                : query.OrderBy(a => a.Name),
            "targetapplication" => filter.OrderDescending 
                ? query.OrderByDescending(a => a.TargetApplication) 
                : query.OrderBy(a => a.TargetApplication),
            "updatedat" => filter.OrderDescending 
                ? query.OrderByDescending(a => a.UpdatedAt) 
                : query.OrderBy(a => a.UpdatedAt),
            _ => filter.OrderDescending 
                ? query.OrderByDescending(a => a.CreatedAt) 
                : query.OrderBy(a => a.CreatedAt)
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

    private static string IncrementVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 3 && int.TryParse(parts[2], out var patch))
        {
            return $"{parts[0]}.{parts[1]}.{patch + 1}";
        }
        return "1.0.1";
    }
}

