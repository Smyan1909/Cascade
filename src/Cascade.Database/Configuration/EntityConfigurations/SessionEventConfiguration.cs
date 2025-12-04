using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for session events.
/// </summary>
public class SessionEventConfiguration : IEntityTypeConfiguration<SessionEvent>
{
    public void Configure(EntityTypeBuilder<SessionEvent> builder)
    {
        builder.ToTable("session_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.AutomationSessionId)
            .HasColumnName("automation_session_id")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .HasDefaultValue("{}");

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at");

        builder.HasIndex(e => e.AutomationSessionId)
            .HasDatabaseName("idx_session_events_session");

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Events)
            .HasForeignKey(e => e.AutomationSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


