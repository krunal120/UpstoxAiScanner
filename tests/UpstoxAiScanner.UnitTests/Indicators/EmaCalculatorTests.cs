using FluentAssertions;
using UpstoxAiScanner.Domain.Calculators;
using UpstoxAiScanner.Domain.Models;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Indicators;

public class EmaCalculatorTests
{
    private static Candle CreateCandle(decimal close, int dayOffset)
    {
        var dt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOffset);
        return new Candle
        {
            Timestamp = dt,
            Open = close,
            High = close + 1m,
            Low = close - 1m,
            Close = close,
            Volume = 50000,
            OpenInterest = 0
        };
    }

    [Fact]
    public void CalculateSeries_FirstValueIsSma_AndSubsequentUseMultiplier()
    {
        // 5 period EMA on 10, 11, 12, 13, 14, 15
        var values = new List<decimal> { 10m, 11m, 12m, 13m, 14m, 15m };
        var ema = EmaCalculator.CalculateSeries(values, 5);

        ema.Should().HaveCount(6);
        ema[0].Should().BeNull();
        ema[3].Should().BeNull();

        // Index 4: SMA(10, 11, 12, 13, 14) = 60 / 5 = 12.0
        ema[4].Should().Be(12.0m);

        // Index 5: Price=15, prevEMA=12, multiplier = 2/(5+1) = 2/6 = 1/3
        // EMA = (15 - 12) * (1/3) + 12 = 1 + 12 = 13.0
        ema[5].Should().Be(13.0m);
    }

    [Fact]
    public void Analyze_BullishAlignment_IdentifiesWhenEma20GreaterThan50GreaterThan200()
    {
        // 250 candles of steadily increasing prices
        var candles = Enumerable.Range(0, 250)
            .Select(i => CreateCandle(100m + (i * 1.5m), i))
            .ToList();

        var analysis = EmaCalculator.Analyze(candles);

        analysis.Should().NotBeNull();
        analysis!.Ema20.Should().NotBeNull();
        analysis.Ema50.Should().NotBeNull();
        analysis.Ema200.Should().NotBeNull();

        analysis.Ema20!.Value.Should().BeGreaterThan(analysis.Ema50!.Value);
        analysis.Ema50.Value.Should().BeGreaterThan(analysis.Ema200!.Value);
        analysis.IsBullishAlignment.Should().BeTrue();
        analysis.IsBearishAlignment.Should().BeFalse();
        analysis.IsPriceAboveEma20.Should().BeTrue();
    }

    [Fact]
    public void Analyze_BearishAlignment_IdentifiesWhenEma20LessThan50LessThan200()
    {
        // 250 candles of steadily falling prices
        var candles = Enumerable.Range(0, 250)
            .Select(i => CreateCandle(500m - (i * 1.5m), i))
            .ToList();

        var analysis = EmaCalculator.Analyze(candles);

        analysis.Should().NotBeNull();
        analysis!.IsBearishAlignment.Should().BeTrue();
        analysis.IsBullishAlignment.Should().BeFalse();
        analysis.IsPriceAboveEma20.Should().BeFalse();
    }

    [Fact]
    public void Analyze_CrossedAboveEma20_DetectsCrossingEvent()
    {
        // 25 candles around 100, then dip below EMA20, then strong breakout above
        var prices = new List<decimal>();
        for (int i = 0; i < 20; i++) prices.Add(100m);
        prices.Add(95m); // prev candle below EMA20
        prices.Add(105m); // current candle jumps above EMA20

        var candles = prices.Select((p, idx) => CreateCandle(p, idx)).ToList();
        var analysis = EmaCalculator.Analyze(candles);

        analysis.Should().NotBeNull();
        analysis!.CrossedAboveEma20.Should().BeTrue();
    }
}
