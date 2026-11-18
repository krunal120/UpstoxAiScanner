namespace UpstoxAiScanner.Domain.Models;

/// <summary>
/// Full market quote snapshot representing complete market data.
/// </summary>
public record MarketQuote
{
    public required string InstrumentKey { get; init; }
    public required string Symbol { get; init; }
    public decimal LastPrice { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal NetChange { get; init; }
    public long TotalBuyQuantity { get; init; }
    public long TotalSellQuantity { get; init; }
    public decimal LowerCircuitLimit { get; init; }
    public decimal UpperCircuitLimit { get; init; }
    public decimal PrevClosePrice { get; init; }
    public decimal YearHigh { get; init; }
    public decimal YearLow { get; init; }
    public decimal OpenInterest { get; init; }
    public DateTimeOffset? LastTradeTime { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public MarketDepth Depth { get; init; } = new();
}
