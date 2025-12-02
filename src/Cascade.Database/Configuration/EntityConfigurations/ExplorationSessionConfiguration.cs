using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the ExplorationSession entity.
/// </summary>
public class ExplorationSessionConfiguration : IEntityTypeConfiguration<ExplorationSession>
{
    public void Configure(EntityTypeBuilder<ExplorationSession> builder)
    {
        builder.ToTable("exploration_sessions");

        builder.HasKey(es => es.Id);

        builder.Property(es => es.Id)
            .HasColumnName("id");

        builder.Property(es => es.TargetApplication)
            .HasColumnName("target_application")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(es => es.InstructionManual)
            .HasColumnName("instruction_manual");

        builder.Property(es => es.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(es => es.Progress)
            .HasColumnName("progress")
            .HasDefaultValue(0);

        builder.Property(es => es.Goals)
            .HasColumnName("goals")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ExplorationGoal>>(v, (JsonSerializerOptions?)null) ?? new List<ExplorationGoal>());

        builder.Property(es => es.CompletedGoals)
            .HasColumnName("completed_goals")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.Property(es => es.FailedGoals)
            .HasColumnName("failed_goals")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.Property(es => es.StartedAt)
            .HasColumnName("started_at");

        builder.Property(es => es.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(es => es.GeneratedAgentId)
            .HasColumnName("generated_agent_id");

        // Indexes
        builder.HasIndex(es => es.Status);

        builder.HasIndex(es => es.TargetApplication);

        // Relationships
        builder.HasOne(es => es.GeneratedAgent)
            .WithMany()
            .HasForeignKey(es => es.GeneratedAgentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

