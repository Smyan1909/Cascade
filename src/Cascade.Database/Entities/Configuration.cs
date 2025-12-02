using Cascade.Database.Enums;

namespace Cascade.Database.Entities;

/// <summary>
/// Represents a configuration key-value pair.
/// </summary>
public class Configuration
{
    /// <summary>
    /// Configuration key (primary key).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Configuration value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the configuration.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Data type of the configuration value.
    /// </summary>
    public ConfigurationType Type { get; set; }

    /// <summary>
    /// Whether the value is encrypted.
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// When the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

