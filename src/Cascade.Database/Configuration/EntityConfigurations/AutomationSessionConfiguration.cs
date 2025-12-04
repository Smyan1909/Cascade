using System.Text.Json;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for automation sessions.
/// </summary>
public class AutomationSessionConfiguration : IEntityTypeConfiguration<AutomationSession>
{
    public void Configure(EntityTypeBuilder<AutomationSession> builder)
    {
        builder.ToTable("automation_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.SessionId)
            .HasColumnName("session_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        builder.Property(s => s.ExecutionRecordId)
            .HasColumnName("execution_record_id");

        builder.Property(s => s.RunId)
            .HasColumnName("run_id")
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(s => s.Profile)
            .HasColumnName("profile")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<VirtualDesktopProfile>(v, (JsonSerializerOptions?)null) ?? new VirtualDesktopProfile());

        builder.Property(s => s.State)
            .HasColumnName("state")
            .HasMaxLength(32)
            .HasConversion<string>()
            .HasDefaultValue(SessionState.Active);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(s => s.ReleasedAt)
            .HasColumnName("released_at");

        builder.HasIndex(s => s.SessionId)
            .IsUnique();

        builder.HasIndex(s => s.AgentId);

        builder.HasOne(s => s.Agent)
            .WithMany(a => a.Sessions)
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ExecutionRecord)
            .WithMany(r => r.Sessions)
            .HasForeignKey(s => s.ExecutionRecordId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}


