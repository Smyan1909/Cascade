using Cascade.Database.Configuration;
using Cascade.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cascade.Database;

/// <summary>
/// Utility class for running database migrations.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Runs pending migrations if AutoMigrate is enabled.
    /// </summary>
    /// <param name="services">The service provider.</param>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CascadeDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (options.AutoMigrate)
        {
            await context.Database.MigrateAsync();
        }
    }

    /// <summary>
    /// Ensures the database is created (for SQLite without migrations).
    /// </summary>
    /// <param name="services">The service provider.</param>
    public static async Task EnsureCreatedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CascadeDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Deletes and recreates the database (use with caution!).
    /// </summary>
    /// <param name="services">The service provider.</param>
    public static async Task ResetDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CascadeDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}

