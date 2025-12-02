using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the AgentVersion entity.
/// </summary>
public class AgentVersionConfiguration : IEntityTypeConfiguration<AgentVersion>
{
    public void Configure(EntityTypeBuilder<AgentVersion> builder)
    {
        builder.ToTable("agent_versions");

        builder.HasKey(av => av.Id);

        builder.Property(av => av.Id)
            .HasColumnName("id");

        builder.Property(av => av.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        builder.Property(av => av.Version)
            .HasColumnName("version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(av => av.Notes)
            .HasColumnName("notes");

        builder.Property(av => av.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(false);

        builder.Property(av => av.InstructionListSnapshot)
            .HasColumnName("instruction_list_snapshot");

        builder.Property(av => av.CapabilitiesSnapshot)
            .HasColumnName("capabilities_snapshot")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.Property(av => av.ScriptIdsSnapshot)
            .HasColumnName("script_ids_snapshot")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

        builder.Property(av => av.CreatedAt)
            .HasColumnName("created_at");

        // Indexes
        builder.HasIndex(av => av.AgentId);

        builder.HasIndex(av => new { av.AgentId, av.Version })
            .IsUnique();

        // Relationships
        builder.HasOne(av => av.Agent)
            .WithMany(a => a.Versions)
            .HasForeignKey(av => av.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

