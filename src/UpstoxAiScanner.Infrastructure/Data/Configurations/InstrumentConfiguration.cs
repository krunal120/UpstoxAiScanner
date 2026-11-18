using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UpstoxAiScanner.Domain.Entities;

namespace UpstoxAiScanner.Infrastructure.Data.Configurations;

public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("Instruments");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Symbol)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.CompanyName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(i => i.InstrumentKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.Exchange)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("NSE");

        builder.Property(i => i.Segment)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("NSE_EQ");

        builder.Property(i => i.InstrumentType)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("EQ");

        builder.Property(i => i.Isin)
            .HasMaxLength(50);

        builder.Property(i => i.Universe)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("NIFTY500");

        builder.Property(i => i.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Unique index on InstrumentKey
        builder.HasIndex(i => i.InstrumentKey)
            .IsUnique();

        // Query performance indexes
        builder.HasIndex(i => i.Symbol);
        builder.HasIndex(i => i.Universe);
        builder.HasIndex(i => i.IsActive);
        builder.HasIndex(i => new { i.Universe, i.IsActive });

        // Relationships
        builder.HasMany(i => i.HistoricalCandles)
            .WithOne(c => c.Instrument)
            .HasForeignKey(c => c.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.TechnicalIndicators)
            .WithOne(t => t.Instrument)
            .HasForeignKey(t => t.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.ScanResults)
            .WithOne(s => s.Instrument)
            .HasForeignKey(s => s.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
