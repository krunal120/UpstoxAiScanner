namespace UpstoxAiScanner.Domain.Entities;

/// <summary>
/// Persisted technical indicator values for a specific instrument, interval, and timestamp.
/// </summary>
public class TechnicalIndicator
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    public required string Interval { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public decimal? RSI { get; set; }
    public decimal? MACD { get; set; }
    public decimal? MACDSignal { get; set; }
    public decimal? MACDHistogram { get; set; }
    public decimal? EMA20 { get; set; }
    public decimal? EMA50 { get; set; }
    public decimal? EMA200 { get; set; }
    public decimal? AverageVolume { get; set; }
    public decimal? RelativeVolume { get; set; }
}
