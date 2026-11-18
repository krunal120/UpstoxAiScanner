using Microsoft.EntityFrameworkCore;
using UpstoxAiScanner.Domain.Entities;

namespace UpstoxAiScanner.Infrastructure.Data;

public class UpstoxScannerDbContext : DbContext
{
    public UpstoxScannerDbContext(DbContextOptions<UpstoxScannerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<HistoricalCandle> HistoricalCandles => Set<HistoricalCandle>();
    public DbSet<TechnicalIndicator> TechnicalIndicators => Set<TechnicalIndicator>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UpstoxScannerDbContext).Assembly);
    }
}
