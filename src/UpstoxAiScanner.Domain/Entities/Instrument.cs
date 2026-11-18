namespace UpstoxAiScanner.Domain.Entities;

/// <summary>
/// Stock instrument entity representing a tradable asset.
/// </summary>
public class Instrument
{
    public int Id { get; set; }
    public required string Symbol { get; set; }
    public required string CompanyName { get; set; }
    public required string InstrumentKey { get; set; }
    public string Exchange { get; set; } = "NSE";
    public string Segment { get; set; } = "NSE_EQ";
    public string InstrumentType { get; set; } = "EQ";
    public string? Isin { get; set; }
    public string Universe { get; set; } = "NIFTY500";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<HistoricalCandle> HistoricalCandles { get; set; } = new List<HistoricalCandle>();
    public ICollection<TechnicalIndicator> TechnicalIndicators { get; set; } = new List<TechnicalIndicator>();
    public ICollection<ScanResult> ScanResults { get; set; } = new List<ScanResult>();
}
