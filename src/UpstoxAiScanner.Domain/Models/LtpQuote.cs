namespace UpstoxAiScanner.Domain.Models;

/// <summary>
/// Domain model for Last Traded Price (LTP) quote.
/// </summary>
public record LtpQuote
{
    public required string InstrumentKey { get; init; }
    public decimal LastPrice { get; init; }
    public long LastTradedQuantity { get; init; }
    public long Volume { get; init; }
    public decimal ClosePrice { get; init; }
}
