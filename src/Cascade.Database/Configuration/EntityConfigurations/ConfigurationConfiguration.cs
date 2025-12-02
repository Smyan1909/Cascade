using Cascade.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cascade.Database.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for the Configuration entity.
/// </summary>
public class ConfigurationConfiguration : IEntityTypeConfiguration<Entities.Configuration>
{
    public void Configure(EntityTypeBuilder<Entities.Configuration> builder)
    {
        builder.ToTable("configurations");

        builder.HasKey(c => c.Key);

        builder.Property(c => c.Key)
            .HasColumnName("key")
            .HasMaxLength(255);

        builder.Property(c => c.Value)
            .HasColumnName("value")
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description");

        builder.Property(c => c.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(Enums.ConfigurationType.String);

        builder.Property(c => c.IsEncrypted)
            .HasColumnName("is_encrypted")
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");
    }
}

