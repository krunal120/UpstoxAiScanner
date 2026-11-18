namespace UpstoxAiScanner.Domain.Entities;

/// <summary>
/// Persisted historical or intraday OHLCV candle bar.
/// </summary>
public class HistoricalCandle
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>
    /// Candle interval: "1d", "5m", "1m", "15m", etc.
    /// </summary>
    public required string Interval { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal OpenInterest { get; set; }
}
