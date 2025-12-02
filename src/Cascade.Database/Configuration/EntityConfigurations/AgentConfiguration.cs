using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the Agent entity.
/// </summary>
public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("agents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasColumnName("description");

        builder.Property(a => a.TargetApplication)
            .HasColumnName("target_application")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.ActiveVersion)
            .HasColumnName("active_version")
            .HasMaxLength(50)
            .HasDefaultValue("1.0.0");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(a => a.Capabilities)
            .HasColumnName("capabilities")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.Property(a => a.InstructionList)
            .HasColumnName("instruction_list");

        builder.Property(a => a.Metadata)
            .HasColumnName("metadata")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(a => a.LastExecutedAt)
            .HasColumnName("last_executed_at");

        // Indexes
        builder.HasIndex(a => a.Name)
            .IsUnique();

        builder.HasIndex(a => a.TargetApplication);

        builder.HasIndex(a => a.Status);

        // Relationships configured via navigation properties
    }
}

