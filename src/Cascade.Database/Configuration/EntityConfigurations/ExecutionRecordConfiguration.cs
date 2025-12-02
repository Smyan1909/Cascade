using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the ExecutionRecord entity.
/// </summary>
public class ExecutionRecordConfiguration : IEntityTypeConfiguration<ExecutionRecord>
{
    public void Configure(EntityTypeBuilder<ExecutionRecord> builder)
    {
        builder.ToTable("execution_records");

        builder.HasKey(er => er.Id);

        builder.Property(er => er.Id)
            .HasColumnName("id");

        builder.Property(er => er.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        builder.Property(er => er.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(255);

        builder.Property(er => er.SessionId)
            .HasColumnName("session_id")
            .HasMaxLength(255);

        builder.Property(er => er.TaskDescription)
            .HasColumnName("task_description")
            .IsRequired();

        builder.Property(er => er.Success)
            .HasColumnName("success")
            .HasDefaultValue(false);

        builder.Property(er => er.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(er => er.Summary)
            .HasColumnName("summary");

        builder.Property(er => er.StartedAt)
            .HasColumnName("started_at");

        builder.Property(er => er.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(er => er.DurationMs)
            .HasColumnName("duration_ms")
            .HasDefaultValue(0);

        builder.Property(er => er.ResultData)
            .HasColumnName("result_data");

        builder.Property(er => er.Logs)
            .HasColumnName("logs")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        // Indexes
        builder.HasIndex(er => er.AgentId);

        builder.HasIndex(er => er.UserId);

        builder.HasIndex(er => er.StartedAt);

        // Relationships
        builder.HasOne(er => er.Agent)
            .WithMany(a => a.Executions)
            .HasForeignKey(er => er.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

