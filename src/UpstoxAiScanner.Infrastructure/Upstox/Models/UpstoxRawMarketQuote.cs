using System.Text.Json;
using System.Text.Json.Serialization;

namespace UpstoxAiScanner.Infrastructure.Upstox.Models;

public class UpstoxRawOhlcSubData
{
    [JsonPropertyName("open")]
    public decimal? Open { get; set; }

    [JsonPropertyName("high")]
    public decimal? High { get; set; }

    [JsonPropertyName("low")]
    public decimal? Low { get; set; }

    [JsonPropertyName("close")]
    public decimal? Close { get; set; }

    [JsonPropertyName("volume")]
    public decimal? Volume { get; set; }

    [JsonPropertyName("ts")]
    public JsonElement? Timestamp { get; set; }
}

public class UpstoxRawDepthItem
{
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("orders")]
    public int Orders { get; set; }
}

public class UpstoxRawDepthData
{
    [JsonPropertyName("buy")]
    public List<UpstoxRawDepthItem>? Buy { get; set; }

    [JsonPropertyName("sell")]
    public List<UpstoxRawDepthItem>? Sell { get; set; }
}

public class UpstoxRawMarketQuote
{
    [JsonPropertyName("ohlc")]
    public UpstoxRawOhlcSubData? Ohlc { get; set; }

    [JsonPropertyName("depth")]
    public UpstoxRawDepthData? Depth { get; set; }

    [JsonPropertyName("timestamp")]
    public JsonElement? Timestamp { get; set; }

    [JsonPropertyName("instrument_token")]
    public string? InstrumentToken { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("last_price")]
    public decimal? LastPrice { get; set; }

    [JsonPropertyName("volume")]
    public decimal? Volume { get; set; }

    [JsonPropertyName("average_price")]
    public decimal? AveragePrice { get; set; }

    [JsonPropertyName("oi")]
    public decimal? OpenInterest { get; set; }

    [JsonPropertyName("net_change")]
    public decimal? NetChange { get; set; }

    [JsonPropertyName("total_buy_quantity")]
    public decimal? TotalBuyQuantity { get; set; }

    [JsonPropertyName("total_sell_quantity")]
    public decimal? TotalSellQuantity { get; set; }

    [JsonPropertyName("lower_circuit_limit")]
    public decimal? LowerCircuitLimit { get; set; }

    [JsonPropertyName("upper_circuit_limit")]
    public decimal? UpperCircuitLimit { get; set; }

    [JsonPropertyName("last_trade_time")]
    public JsonElement? LastTradeTime { get; set; }

    [JsonPropertyName("prev_close_price")]
    public decimal? PrevClosePrice { get; set; }

    [JsonPropertyName("year_high")]
    public decimal? YearHigh { get; set; }

    [JsonPropertyName("year_low")]
    public decimal? YearLow { get; set; }
}

public class UpstoxRawLtpQuote
{
    [JsonPropertyName("last_price")]
    public decimal? LastPrice { get; set; }

    [JsonPropertyName("instrument_token")]
    public string? InstrumentToken { get; set; }

    [JsonPropertyName("ltq")]
    public decimal? LastTradedQuantity { get; set; }

    [JsonPropertyName("volume")]
    public decimal? Volume { get; set; }

    [JsonPropertyName("cp")]
    public decimal? ClosePrice { get; set; }
}

public class UpstoxRawOhlcQuote
{
    [JsonPropertyName("live_ohlc")]
    public UpstoxRawOhlcSubData? LiveOhlc { get; set; }

    [JsonPropertyName("ohlc")]
    public UpstoxRawOhlcSubData? Ohlc { get; set; }

    [JsonPropertyName("prev_ohlc")]
    public UpstoxRawOhlcSubData? PrevOhlc { get; set; }

    [JsonPropertyName("last_price")]
    public decimal? LastPrice { get; set; }

    [JsonPropertyName("instrument_token")]
    public string? InstrumentToken { get; set; }
}

public class UpstoxRawCandlesData
{
    [JsonPropertyName("candles")]
    public List<List<System.Text.Json.JsonElement>>? Candles { get; set; }
}
