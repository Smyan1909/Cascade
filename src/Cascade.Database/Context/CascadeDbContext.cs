using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cascade.Database.Context;

/// <summary>
/// Entity Framework Core database context for Cascade.
/// </summary>
public class CascadeDbContext : DbContext
{
    /// <summary>
    /// Agents collection.
    /// </summary>
    public DbSet<Agent> Agents => Set<Agent>();

    /// <summary>
    /// Agent versions collection.
    /// </summary>
    public DbSet<AgentVersion> AgentVersions => Set<AgentVersion>();

    /// <summary>
    /// Scripts collection.
    /// </summary>
    public DbSet<Script> Scripts => Set<Script>();

    /// <summary>
    /// Script versions collection.
    /// </summary>
    public DbSet<ScriptVersion> ScriptVersions => Set<ScriptVersion>();

    /// <summary>
    /// Exploration sessions collection.
    /// </summary>
    public DbSet<ExplorationSession> ExplorationSessions => Set<ExplorationSession>();

    /// <summary>
    /// Exploration results collection.
    /// </summary>
    public DbSet<ExplorationResult> ExplorationResults => Set<ExplorationResult>();

    /// <summary>
    /// Execution records collection.
    /// </summary>
    public DbSet<ExecutionRecord> ExecutionRecords => Set<ExecutionRecord>();

    /// <summary>
    /// Execution steps collection.
    /// </summary>
    public DbSet<ExecutionStep> ExecutionSteps => Set<ExecutionStep>();

    /// <summary>
    /// Configuration entries collection.
    /// </summary>
    public DbSet<Entities.Configuration> Configurations => Set<Entities.Configuration>();

    /// <summary>
    /// Creates a new instance of the CascadeDbContext.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public CascadeDbContext(DbContextOptions<CascadeDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Configures the model using Fluent API.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CascadeDbContext).Assembly);
    }

    /// <summary>
    /// Override SaveChanges to automatically set timestamps.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Override SaveChangesAsync to automatically set timestamps.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates CreatedAt and UpdatedAt timestamps for tracked entities.
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            // Handle CreatedAt for new entities
            if (entry.State == EntityState.Added)
            {
                var createdAtProperty = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "CreatedAt");
                
                if (createdAtProperty != null && createdAtProperty.CurrentValue is DateTime dt && dt == default)
                {
                    createdAtProperty.CurrentValue = now;
                }

                // Also set StartedAt for exploration sessions
                var startedAtProperty = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "StartedAt");
                
                if (startedAtProperty != null && startedAtProperty.CurrentValue is DateTime st && st == default)
                {
                    startedAtProperty.CurrentValue = now;
                }

                // Also set CapturedAt for exploration results
                var capturedAtProperty = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "CapturedAt");
                
                if (capturedAtProperty != null && capturedAtProperty.CurrentValue is DateTime ct && ct == default)
                {
                    capturedAtProperty.CurrentValue = now;
                }
            }

            // Handle UpdatedAt for all modified entities
            var updatedAtProperty = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
            
            if (updatedAtProperty != null)
            {
                updatedAtProperty.CurrentValue = now;
            }
        }
    }
}

