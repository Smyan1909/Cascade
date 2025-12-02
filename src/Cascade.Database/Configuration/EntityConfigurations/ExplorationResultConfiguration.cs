using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the ExplorationResult entity.
/// </summary>
public class ExplorationResultConfiguration : IEntityTypeConfiguration<ExplorationResult>
{
    public void Configure(EntityTypeBuilder<ExplorationResult> builder)
    {
        builder.ToTable("exploration_results");

        builder.HasKey(er => er.Id);

        builder.Property(er => er.Id)
            .HasColumnName("id");

        builder.Property(er => er.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(er => er.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(er => er.WindowTitle)
            .HasColumnName("window_title")
            .HasMaxLength(255);

        builder.Property(er => er.ElementData)
            .HasColumnName("element_data");

        builder.Property(er => er.ActionTestResult)
            .HasColumnName("action_test_result");

        builder.Property(er => er.NavigationPath)
            .HasColumnName("navigation_path");

        builder.Property(er => er.Screenshot)
            .HasColumnName("screenshot");

        builder.Property(er => er.OcrText)
            .HasColumnName("ocr_text");

        builder.Property(er => er.CapturedAt)
            .HasColumnName("captured_at");

        // Indexes
        builder.HasIndex(er => er.SessionId);

        builder.HasIndex(er => er.Type);

        // Relationships
        builder.HasOne(er => er.Session)
            .WithMany(es => es.Results)
            .HasForeignKey(er => er.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

