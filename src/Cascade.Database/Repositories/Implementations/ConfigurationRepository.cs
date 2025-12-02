using Cascade.Database.Context;
using Cascade.Database.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Database.Repositories.Implementations;

/// <summary>
/// Implementation of the configuration repository.
/// </summary>
public class ConfigurationRepository : IConfigurationRepository
{
    private readonly CascadeDbContext _context;

    public ConfigurationRepository(CascadeDbContext context)
    {
        _context = context;
    }

    public async Task<Entities.Configuration?> GetAsync(string key)
    {
        return await _context.Configurations.FindAsync(key);
    }

    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        var config = await _context.Configurations.FindAsync(key);
        return config?.Value ?? defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0)
    {
        var config = await _context.Configurations.FindAsync(key);
        if (config == null || !int.TryParse(config.Value, out var value))
        {
            return defaultValue;
        }
        return value;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
    {
        var config = await _context.Configurations.FindAsync(key);
        if (config == null || !bool.TryParse(config.Value, out var value))
        {
            return defaultValue;
        }
        return value;
    }

    public async Task SetAsync(string key, string value, string? description = null,
        ConfigurationType type = ConfigurationType.String, bool isEncrypted = false)
    {
        var config = await _context.Configurations.FindAsync(key);
        var now = DateTime.UtcNow;

        if (config == null)
        {
            config = new Entities.Configuration
            {
                Key = key,
                Value = value,
                Description = description,
                Type = type,
                IsEncrypted = isEncrypted,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.Configurations.Add(config);
        }
        else
        {
            config.Value = value;
            if (description != null)
            {
                config.Description = description;
            }
            config.Type = type;
            config.IsEncrypted = isEncrypted;
            config.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string key)
    {
        var config = await _context.Configurations.FindAsync(key);
        if (config != null)
        {
            _context.Configurations.Remove(config);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<Entities.Configuration>> GetAllAsync()
    {
        return await _context.Configurations
            .OrderBy(c => c.Key)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _context.Configurations.AnyAsync(c => c.Key == key);
    }
}

