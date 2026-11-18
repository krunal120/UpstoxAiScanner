using FluentAssertions;
using UpstoxAiScanner.Domain.Calculators;
using UpstoxAiScanner.Domain.Models;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Indicators;

public class RsiCalculatorTests
{
    private static Candle CreateCandle(decimal close, int dayOffset)
    {
        var dt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOffset);
        return new Candle
        {
            Timestamp = dt,
            Open = close - 1m,
            High = close + 2m,
            Low = close - 2m,
            Close = close,
            Volume = 100000,
            OpenInterest = 0
        };
    }

    [Fact]
    public void CalculateSeries_InsufficientCandles_ReturnsNullValues()
    {
        // 14 periods requires at least 15 candles (14 price changes)
        var candles = Enumerable.Range(0, 10).Select(i => CreateCandle(100 + i, i)).ToList();
        var series = RsiCalculator.CalculateSeries(candles, 14);

        series.Should().HaveCount(10);
        series.Should().AllSatisfy(v => v.Should().BeNull());
    }

    [Fact]
    public void CalculateSeries_MonotonicallyIncreasingPrices_Returns100Rsi()
    {
        // All gains, 0 losses
        var candles = Enumerable.Range(0, 25).Select(i => CreateCandle(100 + (i * 2), i)).ToList();
        var series = RsiCalculator.CalculateSeries(candles, 14);

        series[14].Should().Be(100m);
        series[^1].Should().Be(100m);

        var analysis = RsiCalculator.Analyze(candles, 14);
        analysis.Should().NotBeNull();
        analysis!.Value.Should().Be(100m);
        analysis.IsOverbought.Should().BeTrue();
        analysis.IsOversold.Should().BeFalse();
    }

    [Fact]
    public void CalculateSeries_MonotonicallyDecreasingPrices_Returns0Rsi()
    {
        // 0 gains, all losses
        var candles = Enumerable.Range(0, 25).Select(i => CreateCandle(200 - (i * 2), i)).ToList();
        var series = RsiCalculator.CalculateSeries(candles, 14);

        series[14].Should().Be(0m);
        series[^1].Should().Be(0m);

        var analysis = RsiCalculator.Analyze(candles, 14);
        analysis.Should().NotBeNull();
        analysis!.Value.Should().Be(0m);
        analysis.IsOversold.Should().BeTrue();
        analysis.IsOverbought.Should().BeFalse();
    }

    [Fact]
    public void Analyze_CrossedAbove30_DetectsCrossing()
    {
        // Construct prices so RSI transitions from <= 30 to > 30
        var prices = new List<decimal>
        {
            100, 95, 90, 85, 80, 75, 70, 65, 60, 55, 50, 48, 46, 44, 42, // very oversold
            41, 40, 39, 38, 37, 36, // still depressed
            35, 34, 34, 34, // bottoming
            38, 44, 52 // sharp bounce
        };

        var candles = prices.Select((p, idx) => CreateCandle(p, idx)).ToList();
        var analysis = RsiCalculator.Analyze(candles, 14);

        analysis.Should().NotBeNull();
        analysis!.CrossedAbove30.Should().BeTrue();
    }

    [Fact]
    public void Analyze_CrossedBelow70_DetectsCrossing()
    {
        // 20 days of gains to elevate RSI into overbought (>= 70)
        var prices = new List<decimal>();
        decimal p = 100m;
        prices.Add(p);
        for (int i = 0; i < 25; i++)
        {
            p += 4m;
            prices.Add(p);
        }

        // Add downward days until RSI crosses below 70
        while (true)
        {
            var testCandles = prices.Select((x, idx) => CreateCandle(x, idx)).ToList();
            var rsi = RsiCalculator.CalculateSeries(testCandles, 14);
            if (rsi[^1].HasValue && rsi[^1]!.Value >= 70m)
            {
                p -= 6m;
                prices.Add(p);
            }
            else
            {
                break;
            }
        }

        var candles = prices.Select((x, idx) => CreateCandle(x, idx)).ToList();
        var analysis = RsiCalculator.Analyze(candles, 14);

        analysis.Should().NotBeNull();
        analysis!.CrossedBelow70.Should().BeTrue();
    }
}
