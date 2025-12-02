using Cascade.Database.Configuration;
using Cascade.Database.Context;
using Cascade.Database.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Tests.Database;

/// <summary>
/// Factory for creating in-memory SQLite database contexts for testing.
/// </summary>
public class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CascadeDbContext> _options;

    public TestDbContextFactory()
    {
        // Create and open a connection to an in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<CascadeDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a new database context instance.
    /// </summary>
    public CascadeDbContext CreateContext()
    {
        return new CascadeDbContext(_options);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

