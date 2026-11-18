using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UpstoxAiScanner.Domain.Entities;

namespace UpstoxAiScanner.Infrastructure.Data.Configurations;

public class TechnicalIndicatorConfiguration : IEntityTypeConfiguration<TechnicalIndicator>
{
    public void Configure(EntityTypeBuilder<TechnicalIndicator> builder)
    {
        builder.ToTable("TechnicalIndicators");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Interval)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(t => t.RSI).HasPrecision(18, 4);
        builder.Property(t => t.MACD).HasPrecision(18, 4);
        builder.Property(t => t.MACDSignal).HasPrecision(18, 4);
        builder.Property(t => t.MACDHistogram).HasPrecision(18, 4);
        builder.Property(t => t.EMA20).HasPrecision(18, 4);
        builder.Property(t => t.EMA50).HasPrecision(18, 4);
        builder.Property(t => t.EMA200).HasPrecision(18, 4);
        builder.Property(t => t.AverageVolume).HasPrecision(18, 4);
        builder.Property(t => t.RelativeVolume).HasPrecision(18, 4);

        // Composite unique index
        builder.HasIndex(t => new { t.InstrumentId, t.Interval, t.Timestamp })
            .IsUnique();

        builder.HasIndex(t => t.Timestamp);
    }
}
