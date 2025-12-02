using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the Script entity.
/// </summary>
public class ScriptConfiguration : IEntityTypeConfiguration<Script>
{
    public void Configure(EntityTypeBuilder<Script> builder)
    {
        builder.ToTable("scripts");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description");

        builder.Property(s => s.SourceCode)
            .HasColumnName("source_code")
            .IsRequired();

        builder.Property(s => s.CurrentVersion)
            .HasColumnName("current_version")
            .HasMaxLength(50)
            .HasDefaultValue("1.0.0");

        builder.Property(s => s.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.CompiledAssembly)
            .HasColumnName("compiled_assembly");

        builder.Property(s => s.LastCompiledAt)
            .HasColumnName("last_compiled_at");

        builder.Property(s => s.CompilationErrors)
            .HasColumnName("compilation_errors");

        builder.Property(s => s.Metadata)
            .HasColumnName("metadata")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

        builder.Property(s => s.TypeName)
            .HasColumnName("type_name")
            .HasMaxLength(255);

        builder.Property(s => s.MethodName)
            .HasColumnName("method_name")
            .HasMaxLength(255);

        builder.Property(s => s.AgentId)
            .HasColumnName("agent_id");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes
        builder.HasIndex(s => s.Name)
            .IsUnique();

        builder.HasIndex(s => s.Type);

        builder.HasIndex(s => s.AgentId);

        // Relationships
        builder.HasOne(s => s.Agent)
            .WithMany(a => a.Scripts)
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

