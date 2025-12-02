using Cascade.Database.Enums;

namespace Cascade.Database.Configuration;

/// <summary>
/// Configuration options for the Cascade database.
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// The database provider to use.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;

    /// <summary>
    /// Connection string for the database.
    /// For SQLite, use "Data Source=cascade.db".
    /// For PostgreSQL, use a standard connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=cascade.db";

    /// <summary>
    /// SQLite file path (alternative to ConnectionString for SQLite).
    /// </summary>
    public string? SqliteFilePath { get; set; }

    /// <summary>
    /// PostgreSQL host name.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// PostgreSQL port number.
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// PostgreSQL database name.
    /// </summary>
    public string? Database { get; set; }

    /// <summary>
    /// PostgreSQL username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// PostgreSQL password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether to use SSL for PostgreSQL connections.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Whether to automatically run migrations on startup.
    /// </summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>
    /// Maximum connection pool size.
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Builds a connection string from individual properties if not directly specified.
    /// </summary>
    /// <returns>The connection string to use.</returns>
    public string GetConnectionString()
    {
        if (!string.IsNullOrEmpty(ConnectionString))
        {
            return ConnectionString;
        }

        if (Provider == DatabaseProvider.SQLite)
        {
            var dbPath = SqliteFilePath ?? "cascade.db";
            return $"Data Source={dbPath}";
        }

        // PostgreSQL
        if (string.IsNullOrEmpty(Host) || string.IsNullOrEmpty(Database))
        {
            throw new InvalidOperationException(
                "PostgreSQL requires Host and Database to be specified.");
        }

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            SslMode = UseSsl ? Npgsql.SslMode.Require : Npgsql.SslMode.Prefer,
            MaxPoolSize = MaxPoolSize,
            CommandTimeout = CommandTimeout
        };

        return builder.ConnectionString;
    }
}

