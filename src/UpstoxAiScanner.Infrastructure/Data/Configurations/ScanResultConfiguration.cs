using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UpstoxAiScanner.Domain.Entities;

namespace UpstoxAiScanner.Infrastructure.Data.Configurations;

public class ScanResultConfiguration : IEntityTypeConfiguration<ScanResult>
{
    public void Configure(EntityTypeBuilder<ScanResult> builder)
    {
        builder.ToTable("ScanResults");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScanType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.CurrentPrice)
            .HasPrecision(18, 4);

        builder.Property(s => s.TechnicalScore)
            .HasPrecision(18, 2);

        builder.Property(s => s.Signal)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.BreakoutStatus)
            .HasMaxLength(50);

        builder.Property(s => s.VolumeStatus)
            .HasMaxLength(50);

        builder.Property(s => s.Explanation)
            .HasMaxLength(2000);

        builder.HasIndex(s => new { s.ScanType, s.Timestamp });
        builder.HasIndex(s => s.TechnicalScore);
        builder.HasIndex(s => s.Signal);
    }
}
