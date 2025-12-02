using Cascade.Database.Enums;

namespace Cascade.Database.Repositories;

/// <summary>
/// Repository interface for configuration key-value pairs.
/// </summary>
public interface IConfigurationRepository
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration entity if found, otherwise null.</returns>
    Task<Entities.Configuration?> GetAsync(string key);

    /// <summary>
    /// Gets a configuration value as a string.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">Default value if not found.</param>
    /// <returns>The configuration value or default.</returns>
    Task<string> GetValueAsync(string key, string defaultValue = "");

    /// <summary>
    /// Gets a configuration value as an integer.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">Default value if not found or invalid.</param>
    /// <returns>The configuration value or default.</returns>
    Task<int> GetIntAsync(string key, int defaultValue = 0);

    /// <summary>
    /// Gets a configuration value as a boolean.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">Default value if not found or invalid.</param>
    /// <returns>The configuration value or default.</returns>
    Task<bool> GetBoolAsync(string key, bool defaultValue = false);

    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="type">The value type.</param>
    /// <param name="isEncrypted">Whether the value should be encrypted.</param>
    Task SetAsync(string key, string value, string? description = null, 
        ConfigurationType type = ConfigurationType.String, bool isEncrypted = false);

    /// <summary>
    /// Deletes a configuration by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    Task DeleteAsync(string key);

    /// <summary>
    /// Gets all configuration entries.
    /// </summary>
    /// <returns>List of all configurations.</returns>
    Task<IReadOnlyList<Entities.Configuration>> GetAllAsync();

    /// <summary>
    /// Checks if a configuration key exists.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>True if the key exists.</returns>
    Task<bool> ExistsAsync(string key);
}

