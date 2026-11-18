using System.Text.Json.Serialization;

namespace UpstoxAiScanner.Infrastructure.Services.Models;

/// <summary>
/// Raw instrument entry from Upstox NSE.json.gz master file.
/// </summary>
public class UpstoxMasterRecord
{
    [JsonPropertyName("trading_symbol")]
    public string? TradingSymbol { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("instrument_key")]
    public string? InstrumentKey { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("segment")]
    public string? Segment { get; set; }

    [JsonPropertyName("instrument_type")]
    public string? InstrumentType { get; set; }

    [JsonPropertyName("isin")]
    public string? Isin { get; set; }

    [JsonPropertyName("lot_size")]
    public int? LotSize { get; set; }

    [JsonPropertyName("freeze_quantity")]
    public decimal? FreezeQuantity { get; set; }

    [JsonPropertyName("tick_size")]
    public decimal? TickSize { get; set; }
}

/// <summary>
/// Constituent stock entry parsed from official Nifty 500 CSV.
/// </summary>
public class Nifty500ConstituentRecord
{
    public string CompanyName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Series { get; set; } = "EQ";
    public string IsinCode { get; set; } = string.Empty;
}
