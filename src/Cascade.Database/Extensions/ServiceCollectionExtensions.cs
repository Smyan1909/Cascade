using Cascade.Database.Configuration;
using Cascade.Database.Context;
using Cascade.Database.Enums;
using Cascade.Database.Repositories;
using Cascade.Database.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cascade.Database.Extensions;

/// <summary>
/// Extension methods for registering Cascade database services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Cascade database services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCascadeDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(options);

        return services.AddCascadeDatabase(options);
    }

    /// <summary>
    /// Adds Cascade database services to the service collection with explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The database options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCascadeDatabase(
        this IServiceCollection services,
        DatabaseOptions options)
    {
        // Register options
        services.Configure<DatabaseOptions>(opt =>
        {
            opt.Provider = options.Provider;
            opt.ConnectionString = options.ConnectionString;
            opt.SqliteFilePath = options.SqliteFilePath;
            opt.Host = options.Host;
            opt.Port = options.Port;
            opt.Database = options.Database;
            opt.Username = options.Username;
            opt.Password = options.Password;
            opt.UseSsl = options.UseSsl;
            opt.SessionLogRetention = options.SessionLogRetention;
            opt.AutoMigrate = options.AutoMigrate;
            opt.MaxPoolSize = options.MaxPoolSize;
            opt.CommandTimeout = options.CommandTimeout;
        });

        // Register DbContext
        services.AddDbContext<CascadeDbContext>(dbOptions =>
        {
            CascadeDbContextFactory.ConfigureDbContext(dbOptions, options);
        });

        // Register repositories
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IScriptRepository, ScriptRepository>();
        services.AddScoped<IExplorationRepository, ExplorationRepository>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();

        return services;
    }

    /// <summary>
    /// Adds Cascade database services with a configure action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure database options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCascadeDatabase(
        this IServiceCollection services,
        Action<DatabaseOptions> configure)
    {
        var options = new DatabaseOptions();
        configure(options);
        return services.AddCascadeDatabase(options);
    }

    /// <summary>
    /// Adds Cascade database services with SQLite using the specified file path.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Path to the SQLite database file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCascadeSqlite(
        this IServiceCollection services,
        string databasePath = "cascade.db")
    {
        return services.AddCascadeDatabase(options =>
        {
            options.Provider = DatabaseProvider.SQLite;
            options.ConnectionString = $"Data Source={databasePath}";
        });
    }

    /// <summary>
    /// Adds Cascade database services with PostgreSQL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCascadePostgreSql(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddCascadeDatabase(options =>
        {
            options.Provider = DatabaseProvider.PostgreSQL;
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Adds Cascade database with in-memory SQLite (useful for testing).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCascadeInMemoryDatabase(
        this IServiceCollection services)
    {
        return services.AddCascadeDatabase(options =>
        {
            options.Provider = DatabaseProvider.SQLite;
            options.ConnectionString = "Data Source=:memory:";
            options.AutoMigrate = false;
        });
    }
}

