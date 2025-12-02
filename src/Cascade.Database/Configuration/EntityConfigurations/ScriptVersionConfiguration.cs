using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the ScriptVersion entity.
/// </summary>
public class ScriptVersionConfiguration : IEntityTypeConfiguration<ScriptVersion>
{
    public void Configure(EntityTypeBuilder<ScriptVersion> builder)
    {
        builder.ToTable("script_versions");

        builder.HasKey(sv => sv.Id);

        builder.Property(sv => sv.Id)
            .HasColumnName("id");

        builder.Property(sv => sv.ScriptId)
            .HasColumnName("script_id")
            .IsRequired();

        builder.Property(sv => sv.Version)
            .HasColumnName("version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(sv => sv.SourceCode)
            .HasColumnName("source_code")
            .IsRequired();

        builder.Property(sv => sv.ChangeDescription)
            .HasColumnName("change_description");

        builder.Property(sv => sv.CompiledAssembly)
            .HasColumnName("compiled_assembly");

        builder.Property(sv => sv.CreatedAt)
            .HasColumnName("created_at");

        // Indexes
        builder.HasIndex(sv => sv.ScriptId);

        builder.HasIndex(sv => new { sv.ScriptId, sv.Version })
            .IsUnique();

        // Relationships
        builder.HasOne(sv => sv.Script)
            .WithMany(s => s.Versions)
            .HasForeignKey(sv => sv.ScriptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

