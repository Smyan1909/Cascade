namespace Cascade.Database.Enums;

/// <summary>
/// Represents the database provider to use.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// SQLite database (local development).
    /// </summary>
    SQLite,

    /// <summary>
    /// PostgreSQL database (production/distributed).
    /// </summary>
    PostgreSQL
}

