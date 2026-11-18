using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UpstoxAiScanner.Domain.Entities;

namespace UpstoxAiScanner.Infrastructure.Data.Configurations;

public class HistoricalCandleConfiguration : IEntityTypeConfiguration<HistoricalCandle>
{
    public void Configure(EntityTypeBuilder<HistoricalCandle> builder)
    {
        builder.ToTable("HistoricalCandles");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Interval)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(c => c.Open)
            .HasPrecision(18, 4);

        builder.Property(c => c.High)
            .HasPrecision(18, 4);

        builder.Property(c => c.Low)
            .HasPrecision(18, 4);

        builder.Property(c => c.Close)
            .HasPrecision(18, 4);

        builder.Property(c => c.OpenInterest)
            .HasPrecision(18, 4);

        // Composite unique index to avoid duplicate candle entries for the same interval and timestamp
        builder.HasIndex(c => new { c.InstrumentId, c.Interval, c.Timestamp })
            .IsUnique();

        // Index for time-range queries
        builder.HasIndex(c => new { c.InstrumentId, c.Interval });
        builder.HasIndex(c => c.Timestamp);
    }
}
