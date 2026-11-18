using FluentAssertions;
using UpstoxAiScanner.Infrastructure.Upstox.Services;
using Xunit;

namespace UpstoxAiScanner.UnitTests;

public class MockUpstoxDataProviderTests
{
    private readonly MockUpstoxDataProvider _provider = new();

    [Fact]
    public void GetQuotes_ShouldReturnAccurateDataForKnownStocks()
    {
        var keys = new[] { "NSE_EQ|INE002A01018", "NSE_EQ|INE467B01029" };

        var quotes = _provider.GetQuotes(keys);

        quotes.Should().HaveCount(2);
        quotes.Should().ContainKey("NSE_EQ|INE002A01018");
        quotes.Should().ContainKey("NSE_EQ|INE467B01029");

        var reliance = quotes["NSE_EQ|INE002A01018"];
        reliance.Symbol.Should().Be("RELIANCE");
        reliance.LastPrice.Should().BeGreaterThan(0);
        reliance.High.Should().BeGreaterThanOrEqualTo(reliance.Low);
        reliance.Volume.Should().BeGreaterThan(0);
        reliance.Depth.Buy.Should().HaveCount(5);
        reliance.Depth.Sell.Should().HaveCount(5);
    }

    [Fact]
    public void GetLtpQuotes_ShouldReturnPricesAndVolumes()
    {
        var keys = new[] { "NSE_EQ|INE002A01018" };

        var ltpQuotes = _provider.GetLtpQuotes(keys);

        ltpQuotes.Should().ContainKey("NSE_EQ|INE002A01018");
        var ltp = ltpQuotes["NSE_EQ|INE002A01018"];
        ltp.LastPrice.Should().BeGreaterThan(0);
        ltp.ClosePrice.Should().BeGreaterThan(0);
        ltp.Volume.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetOhlcQuotes_ShouldReturnCurrentAndPreviousOHLC()
    {
        var keys = new[] { "NSE_EQ|INE009A01021" };

        var ohlcQuotes = _provider.GetOhlcQuotes(keys, "1d");

        ohlcQuotes.Should().ContainKey("NSE_EQ|INE009A01021");
        var quote = ohlcQuotes["NSE_EQ|INE009A01021"];
        quote.Open.Should().BeGreaterThan(0);
        quote.High.Should().BeGreaterThanOrEqualTo(quote.Low);
        quote.PrevOpen.Should().NotBeNull();
        quote.PrevClose.Should().NotBeNull();
    }

    [Fact]
    public void GetHistoricalCandles_ShouldReturnDailyCandlesWithinDateRangeWithoutWeekends()
    {
        var toDate = new DateOnly(2026, 3, 31);
        var fromDate = new DateOnly(2026, 3, 1);

        var candles = _provider.GetHistoricalCandles("NSE_EQ|INE002A01018", "days", 1, toDate, fromDate);

        candles.Should().NotBeEmpty();
        foreach (var candle in candles)
        {
            var dayOfWeek = candle.Timestamp.DayOfWeek;
            dayOfWeek.Should().NotBe(DayOfWeek.Saturday);
            dayOfWeek.Should().NotBe(DayOfWeek.Sunday);
            candle.High.Should().BeGreaterThanOrEqualTo(candle.Low);
            candle.Volume.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void GetIntradayCandles_ShouldReturnValid5MinuteCandles()
    {
        var candles = _provider.GetIntradayCandles("NSE_EQ|INE002A01018", "minutes", 5);

        candles.Should().NotBeEmpty();
        foreach (var candle in candles)
        {
            candle.Open.Should().BeGreaterThan(0);
            candle.High.Should().BeGreaterThanOrEqualTo(candle.Low);
            candle.Volume.Should().BeGreaterThan(0);
        }
    }
}
