using Cascade.Database;
using Cascade.Database.Configuration;
using Cascade.Database.Context;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cascade.Tests.Database;

public class DatabaseMigratorTests : IDisposable
{
    private readonly List<string> _dbFiles = new();

    [Fact]
    public async Task MigrateAsync_ShouldCreateSchema_WhenAutoMigrateEnabled()
    {
        var dbPath = CreateTempDatabasePath();
        using var provider = BuildProvider(dbPath, autoMigrate: true);

        await DatabaseMigrator.MigrateAsync(provider);

        File.Exists(dbPath).Should().BeTrue();
        TableExists(dbPath, "agents").Should().BeTrue();
    }

    [Fact]
    public async Task MigrateAsync_ShouldSkip_WhenAutoMigrateDisabled()
    {
        var dbPath = CreateTempDatabasePath();
        using var provider = BuildProvider(dbPath, autoMigrate: false);

        await DatabaseMigrator.MigrateAsync(provider);

        File.Exists(dbPath).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCreatedAsync_ShouldCreateDatabaseWithoutMigrations()
    {
        var dbPath = CreateTempDatabasePath();
        using var provider = BuildProvider(dbPath, autoMigrate: false);

        await DatabaseMigrator.EnsureCreatedAsync(provider);

        File.Exists(dbPath).Should().BeTrue();
        TableExists(dbPath, "agents").Should().BeTrue();
    }

    [Fact]
    public async Task ResetDatabaseAsync_ShouldRecreateDatabase()
    {
        var dbPath = CreateTempDatabasePath();
        using var provider = BuildProvider(dbPath, autoMigrate: true);

        await DatabaseMigrator.MigrateAsync(provider);
        await SeedAgentAsync(provider, "ResetAgent");

        using (var scopeBefore = provider.CreateScope())
        {
            var context = scopeBefore.ServiceProvider.GetRequiredService<CascadeDbContext>();
            (await context.Agents.CountAsync()).Should().Be(1);
        }

        await DatabaseMigrator.ResetDatabaseAsync(provider);

        using var scopeAfter = provider.CreateScope();
        var resetContext = scopeAfter.ServiceProvider.GetRequiredService<CascadeDbContext>();
        (await resetContext.Agents.CountAsync()).Should().Be(0);
        TableExists(dbPath, "agents").Should().BeTrue();
    }

    public void Dispose()
    {
        foreach (var file in _dbFiles)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup failures in tests.
                }
            }
        }
    }

    private static async Task SeedAgentAsync(ServiceProvider provider, string name)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CascadeDbContext>();
        context.Agents.Add(new Agent
        {
            Name = name,
            TargetApplication = "TestApp"
        });
        await context.SaveChangesAsync();
    }

    private ServiceProvider BuildProvider(string dbPath, bool autoMigrate)
    {
        var databaseOptions = new DatabaseOptions
        {
            Provider = DatabaseProvider.SQLite,
            ConnectionString = $"Data Source={dbPath}",
            AutoMigrate = autoMigrate
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<DatabaseOptions>>(new OptionsWrapper<DatabaseOptions>(databaseOptions));
        services.AddDbContext<CascadeDbContext>(options =>
        {
            CascadeDbContextFactory.ConfigureDbContext(options, databaseOptions);
        });

        return services.BuildServiceProvider();
    }

    private string CreateTempDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cascade_db_{Guid.NewGuid():N}.db");
        _dbFiles.Add(path);
        return path;
    }

    private static bool TableExists(string dbPath, string tableName)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() != null;
    }
}


