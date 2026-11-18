namespace UpstoxAiScanner.Domain.Models;

/// <summary>
/// Domain model for an OHLCV candle bar.
/// </summary>
public record Candle
{
    public required DateTimeOffset Timestamp { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
    public decimal OpenInterest { get; init; }
}
