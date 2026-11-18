namespace UpstoxAiScanner.Application.DTOs.MarketData;

/// <summary>
/// DTO representing Last Traded Price (LTP) market quote.
/// </summary>
public record LtpQuoteDto
{
    public required string InstrumentKey { get; init; }
    public decimal LastPrice { get; init; }
    public long LastTradedQuantity { get; init; }
    public long Volume { get; init; }
    public decimal ClosePrice { get; init; }
    public decimal Change { get; init; }
    public decimal ChangePercentage { get; init; }
}
