using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the ExecutionStep entity.
/// </summary>
public class ExecutionStepConfiguration : IEntityTypeConfiguration<ExecutionStep>
{
    public void Configure(EntityTypeBuilder<ExecutionStep> builder)
    {
        builder.ToTable("execution_steps");

        builder.HasKey(es => es.Id);

        builder.Property(es => es.Id)
            .HasColumnName("id");

        builder.Property(es => es.ExecutionId)
            .HasColumnName("execution_id")
            .IsRequired();

        builder.Property(es => es.Order)
            .HasColumnName("step_order")
            .IsRequired();

        builder.Property(es => es.Action)
            .HasColumnName("action")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(es => es.Parameters)
            .HasColumnName("parameters");

        builder.Property(es => es.Success)
            .HasColumnName("success")
            .HasDefaultValue(false);

        builder.Property(es => es.Error)
            .HasColumnName("error");

        builder.Property(es => es.Result)
            .HasColumnName("result");

        builder.Property(es => es.DurationMs)
            .HasColumnName("duration_ms")
            .HasDefaultValue(0);

        builder.Property(es => es.Screenshot)
            .HasColumnName("screenshot");

        // Indexes
        builder.HasIndex(es => es.ExecutionId);

        // Relationships
        builder.HasOne(es => es.Execution)
            .WithMany(er => er.Steps)
            .HasForeignKey(es => es.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

