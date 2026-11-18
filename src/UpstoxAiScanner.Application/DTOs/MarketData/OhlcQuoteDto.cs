namespace UpstoxAiScanner.Application.DTOs.MarketData;

/// <summary>
/// DTO representing OHLC market quote.
/// </summary>
public record OhlcQuoteDto
{
    public required string InstrumentKey { get; init; }
    public decimal LastPrice { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
    public DateTimeOffset? Timestamp { get; init; }

    public decimal? PrevOpen { get; init; }
    public decimal? PrevHigh { get; init; }
    public decimal? PrevLow { get; init; }
    public decimal? PrevClose { get; init; }
    public long? PrevVolume { get; init; }
}
