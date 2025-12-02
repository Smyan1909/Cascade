using Cascade.Database.Configuration;
using Cascade.Database.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Cascade.Database.Context;

/// <summary>
/// Design-time factory for creating CascadeDbContext instances.
/// Used by EF Core tools for migrations.
/// </summary>
public class CascadeDbContextFactory : IDesignTimeDbContextFactory<CascadeDbContext>
{
    /// <summary>
    /// Creates a new CascadeDbContext instance for design-time operations.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A configured CascadeDbContext instance.</returns>
    public CascadeDbContext CreateDbContext(string[] args)
    {
        // Try to load configuration from appsettings.json if available
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(options);

        var optionsBuilder = new DbContextOptionsBuilder<CascadeDbContext>();

        ConfigureDbContext(optionsBuilder, options);

        return new CascadeDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Configures the DbContext options based on the database options.
    /// </summary>
    /// <param name="optionsBuilder">The options builder to configure.</param>
    /// <param name="options">The database options.</param>
    public static void ConfigureDbContext(
        DbContextOptionsBuilder optionsBuilder,
        DatabaseOptions options)
    {
        var connectionString = options.GetConnectionString();

        switch (options.Provider)
        {
            case DatabaseProvider.SQLite:
                optionsBuilder.UseSqlite(connectionString);
                break;

            case DatabaseProvider.PostgreSQL:
                optionsBuilder.UseNpgsql(connectionString);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(options.Provider),
                    $"Unsupported database provider: {options.Provider}");
        }
    }

    /// <summary>
    /// Creates a DbContext options builder configured for the specified options.
    /// </summary>
    /// <param name="options">The database options.</param>
    /// <returns>Configured DbContextOptions.</returns>
    public static DbContextOptions<CascadeDbContext> CreateOptions(DatabaseOptions options)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CascadeDbContext>();
        ConfigureDbContext(optionsBuilder, options);
        return optionsBuilder.Options;
    }
}

