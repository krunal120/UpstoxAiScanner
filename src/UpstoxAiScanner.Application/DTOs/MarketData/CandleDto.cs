namespace UpstoxAiScanner.Application.DTOs.MarketData;

/// <summary>
/// DTO representing an OHLCV candle bar.
/// </summary>
public record CandleDto
{
    public required DateTimeOffset Timestamp { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
    public decimal OpenInterest { get; init; }
}
