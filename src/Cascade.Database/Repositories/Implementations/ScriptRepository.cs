using Cascade.Database.Context;
using Cascade.Database.Entities;
using Cascade.Database.Filters;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Database.Repositories.Implementations;

/// <summary>
/// Implementation of the script repository.
/// </summary>
public class ScriptRepository : IScriptRepository
{
    private readonly CascadeDbContext _context;

    public ScriptRepository(CascadeDbContext context)
    {
        _context = context;
    }

    public async Task<Script?> GetByIdAsync(Guid id)
    {
        return await _context.Scripts
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Script?> GetByNameAsync(string name)
    {
        return await _context.Scripts
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Name == name);
    }

    public async Task<IReadOnlyList<Script>> GetByAgentIdAsync(Guid agentId)
    {
        return await _context.Scripts
            .Where(s => s.AgentId == agentId)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Script>> GetAllAsync(ScriptFilter? filter = null)
    {
        var query = BuildQuery(filter);
        return await query.ToListAsync();
    }

    public async Task<Script> SaveAsync(Script script)
    {
        if (script.Id == Guid.Empty)
        {
            script.Id = Guid.NewGuid();
            _context.Scripts.Add(script);
        }
        else
        {
            _context.Scripts.Update(script);
        }

        await _context.SaveChangesAsync();
        return script;
    }

    public async Task DeleteAsync(Guid id)
    {
        var script = await _context.Scripts.FindAsync(id);
        if (script != null)
        {
            _context.Scripts.Remove(script);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ScriptVersion> CreateVersionAsync(Guid scriptId, string sourceCode, string? description = null)
    {
        var script = await _context.Scripts.FindAsync(scriptId);
        if (script == null)
        {
            throw new InvalidOperationException($"Script with ID {scriptId} not found.");
        }

        // Parse current version and increment
        var nextVersion = IncrementVersion(script.CurrentVersion);

        var version = new ScriptVersion
        {
            Id = Guid.NewGuid(),
            ScriptId = scriptId,
            Version = nextVersion,
            SourceCode = sourceCode,
            ChangeDescription = description,
            CreatedAt = DateTime.UtcNow
        };

        _context.ScriptVersions.Add(version);

        // Update script's current version and source
        script.CurrentVersion = nextVersion;
        script.SourceCode = sourceCode;

        await _context.SaveChangesAsync();
        return version;
    }

    public async Task<IReadOnlyList<ScriptVersion>> GetVersionsAsync(Guid scriptId)
    {
        return await _context.ScriptVersions
            .Where(v => v.ScriptId == scriptId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<byte[]?> GetCompiledAssemblyAsync(Guid scriptId, string version)
    {
        var scriptVersion = await _context.ScriptVersions
            .FirstOrDefaultAsync(v => v.ScriptId == scriptId && v.Version == version);

        if (scriptVersion?.CompiledAssembly != null)
        {
            return scriptVersion.CompiledAssembly;
        }

        // If no version-specific assembly, check the script itself
        var script = await _context.Scripts.FindAsync(scriptId);
        if (script?.CurrentVersion == version)
        {
            return script.CompiledAssembly;
        }

        return null;
    }

    public async Task SaveCompiledAssemblyAsync(Guid scriptId, string version, byte[] assembly)
    {
        var scriptVersion = await _context.ScriptVersions
            .FirstOrDefaultAsync(v => v.ScriptId == scriptId && v.Version == version);

        if (scriptVersion != null)
        {
            scriptVersion.CompiledAssembly = assembly;
        }

        // Also update the script if this is the current version
        var script = await _context.Scripts.FindAsync(scriptId);
        if (script?.CurrentVersion == version)
        {
            script.CompiledAssembly = assembly;
            script.LastCompiledAt = DateTime.UtcNow;
            script.CompilationErrors = null;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCountAsync(ScriptFilter? filter = null)
    {
        var query = BuildQuery(filter);
        return await query.CountAsync();
    }

    private IQueryable<Script> BuildQuery(ScriptFilter? filter)
    {
        IQueryable<Script> query = _context.Scripts;

        if (filter == null)
        {
            return query.OrderByDescending(s => s.CreatedAt);
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(s => s.Type == filter.Type.Value);
        }

        if (filter.AgentId.HasValue)
        {
            query = query.Where(s => s.AgentId == filter.AgentId.Value);
        }

        if (!string.IsNullOrEmpty(filter.Name))
        {
            query = query.Where(s => s.Name.Contains(filter.Name));
        }

        if (filter.IsCompiled.HasValue)
        {
            query = filter.IsCompiled.Value
                ? query.Where(s => s.CompiledAssembly != null)
                : query.Where(s => s.CompiledAssembly == null);
        }

        if (filter.CreatedAfter.HasValue)
        {
            query = query.Where(s => s.CreatedAt >= filter.CreatedAfter.Value);
        }

        if (filter.CreatedBefore.HasValue)
        {
            query = query.Where(s => s.CreatedAt <= filter.CreatedBefore.Value);
        }

        // Apply ordering
        query = filter.OrderBy?.ToLower() switch
        {
            "name" => filter.OrderDescending 
                ? query.OrderByDescending(s => s.Name) 
                : query.OrderBy(s => s.Name),
            "type" => filter.OrderDescending 
                ? query.OrderByDescending(s => s.Type) 
                : query.OrderBy(s => s.Type),
            "updatedat" => filter.OrderDescending 
                ? query.OrderByDescending(s => s.UpdatedAt) 
                : query.OrderBy(s => s.UpdatedAt),
            _ => filter.OrderDescending 
                ? query.OrderByDescending(s => s.CreatedAt) 
                : query.OrderBy(s => s.CreatedAt)
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

