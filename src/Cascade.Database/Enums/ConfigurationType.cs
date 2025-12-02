namespace Cascade.Database.Enums;

/// <summary>
/// Represents the data type of a configuration value.
/// </summary>
public enum ConfigurationType
{
    /// <summary>
    /// String value.
    /// </summary>
    String,

    /// <summary>
    /// Integer value.
    /// </summary>
    Integer,

    /// <summary>
    /// Boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// JSON object or array value.
    /// </summary>
    Json,

    /// <summary>
    /// Secret/encrypted value.
    /// </summary>
    Secret
}

