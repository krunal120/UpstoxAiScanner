using System.Text.Json;
using FluentAssertions;
using UpstoxAiScanner.Infrastructure.Upstox.Models;
using Xunit;

namespace UpstoxAiScanner.UnitTests;

public class UpstoxApiResponseDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ShouldDeserialize_FullMarketQuotesV3Response()
    {
        const string json = """
        {
          "status": "success",
          "data": {
            "NSE_EQ:RELIANCE": {
              "ohlc": {
                "open": 2975.0,
                "high": 3010.5,
                "low": 2960.0,
                "close": 2995.2,
                "volume": 4500000,
                "ts": "1710000000000"
              },
              "depth": {
                "buy": [
                  { "quantity": 100, "price": 2995.0, "orders": 2 },
                  { "quantity": 250, "price": 2994.5, "orders": 4 }
                ],
                "sell": [
                  { "quantity": 150, "price": 2995.5, "orders": 3 }
                ]
              },
              "timestamp": "2026-03-31T15:30:00+05:30",
              "instrument_token": "NSE_EQ|INE002A01018",
              "symbol": "NSE_EQ:RELIANCE",
              "last_price": 2995.2,
              "volume": 4500000,
              "average_price": 2985.4,
              "oi": 0.0,
              "net_change": 20.2,
              "total_buy_quantity": 350000,
              "total_sell_quantity": 420000,
              "lower_circuit_limit": 2677.5,
              "upper_circuit_limit": 3272.5,
              "last_trade_time": "2026-03-31T15:29:58+05:30",
              "prev_close_price": 2975.0,
              "year_high": 3200.0,
              "year_low": 2220.0
            }
          }
        }
        """;

        var response = JsonSerializer.Deserialize<UpstoxApiResponse<Dictionary<string, UpstoxRawMarketQuote>>>(json, JsonOptions);

        response.Should().NotBeNull();
        response!.Status.Should().Be("success");
        response.Data.Should().ContainKey("NSE_EQ:RELIANCE");

        var quote = response.Data!["NSE_EQ:RELIANCE"];
        quote.LastPrice.Should().Be(2995.2m);
        quote.Ohlc.Should().NotBeNull();
        quote.Ohlc!.High.Should().Be(3010.5m);
        quote.PrevClosePrice.Should().Be(2975.0m);
        quote.Depth.Should().NotBeNull();
        quote.Depth!.Buy.Should().HaveCount(2);
        quote.Depth.Sell.Should().HaveCount(1);
    }

    [Fact]
    public void ShouldDeserialize_LtpQuotesV3Response()
    {
        const string json = """
        {
          "status": "success",
          "data": {
            "NSE_EQ:TCS": {
              "last_price": 4125.50,
              "instrument_token": "NSE_EQ|INE467B01029",
              "ltq": 25,
              "volume": 1850000,
              "cp": 4095.00
            }
          }
        }
        """;

        var response = JsonSerializer.Deserialize<UpstoxApiResponse<Dictionary<string, UpstoxRawLtpQuote>>>(json, JsonOptions);

        response.Should().NotBeNull();
        response!.Status.Should().Be("success");
        response.Data.Should().ContainKey("NSE_EQ:TCS");

        var ltp = response.Data!["NSE_EQ:TCS"];
        ltp.LastPrice.Should().Be(4125.50m);
        ltp.LastTradedQuantity.Should().Be(25);
        ltp.Volume.Should().Be(1850000);
        ltp.ClosePrice.Should().Be(4095.00m);
    }

    [Fact]
    public void ShouldDeserialize_OhlcQuotesV3Response()
    {
        const string json = """
        {
          "status": "success",
          "data": {
            "NSE_EQ:INFY": {
              "ohlc": {
                "open": 1850.00,
                "high": 1890.00,
                "low": 1845.00,
                "close": 1885.50,
                "volume": 5200000
              },
              "prev_ohlc": {
                "open": 1830.00,
                "high": 1855.00,
                "low": 1825.00,
                "close": 1848.00,
                "volume": 4900000
              },
              "last_price": 1885.50,
              "instrument_token": "NSE_EQ|INE009A01021"
            }
          }
        }
        """;

        var response = JsonSerializer.Deserialize<UpstoxApiResponse<Dictionary<string, UpstoxRawOhlcQuote>>>(json, JsonOptions);

        response.Should().NotBeNull();
        response!.Status.Should().Be("success");
        var ohlc = response.Data!["NSE_EQ:INFY"];
        ohlc.LastPrice.Should().Be(1885.50m);
        ohlc.Ohlc!.Close.Should().Be(1885.50m);
        ohlc.PrevOhlc!.Close.Should().Be(1848.00m);
    }

    [Fact]
    public void ShouldDeserialize_HistoricalCandlesV3Response()
    {
        const string json = """
        {
          "status": "success",
          "data": {
            "candles": [
              [
                "2026-03-31T00:00:00+05:30",
                2980.00,
                3015.00,
                2965.00,
                3002.50,
                4200000,
                0
              ],
              [
                "2026-03-30T00:00:00+05:30",
                2950.00,
                2985.00,
                2940.00,
                2978.00,
                3800000,
                0
              ]
            ]
          }
        }
        """;

        var response = JsonSerializer.Deserialize<UpstoxApiResponse<UpstoxRawCandlesData>>(json, JsonOptions);

        response.Should().NotBeNull();
        response!.Status.Should().Be("success");
        response.Data!.Candles.Should().HaveCount(2);

        var firstCandle = response.Data.Candles![0];
        firstCandle[0].GetString().Should().Be("2026-03-31T00:00:00+05:30");
        firstCandle[1].GetDecimal().Should().Be(2980.00m); // Open
        firstCandle[2].GetDecimal().Should().Be(3015.00m); // High
        firstCandle[3].GetDecimal().Should().Be(2965.00m); // Low
        firstCandle[4].GetDecimal().Should().Be(3002.50m); // Close
        firstCandle[5].GetInt64().Should().Be(4200000);   // Volume
    }
}
