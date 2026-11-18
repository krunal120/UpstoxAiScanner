namespace UpstoxAiScanner.Application.DTOs.MarketData;

/// <summary>
/// Real-time price and volume tick received via Upstox Market Data WebSocket V3 feed.
/// </summary>
public record MarketTickDto
{
    public required string InstrumentKey { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public decimal LastPrice { get; init; }
    public long Volume { get; init; }
    public decimal Change { get; init; }
    public decimal PercentageChange { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public bool IsMock { get; init; }
}
