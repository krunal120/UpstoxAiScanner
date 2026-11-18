using FluentAssertions;
using UpstoxAiScanner.Domain.Calculators;
using UpstoxAiScanner.Domain.Models;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Indicators;

public class BreakoutCalculatorTests
{
    private static Candle CreateCandle(decimal high, decimal low, decimal close, long volume, int dayOffset)
    {
        var dt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOffset);
        return new Candle
        {
            Timestamp = dt,
            Open = (high + low) / 2m,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            OpenInterest = 0
        };
    }

    [Fact]
    public void Analyze_20PeriodHighBreakout_DetectsWhenCloseExceedsPrior20HighsWithoutLookaheadBias()
    {
        // 20 candles with High capped at 100
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
        {
            candles.Add(CreateCandle(high: 100m, low: 90m, close: 95m, volume: 10000, dayOffset: i));
        }

        // 21st candle (current): breaks out with Close = 105, Volume = 50000
        candles.Add(CreateCandle(high: 108m, low: 98m, close: 105m, volume: 50000, dayOffset: 20));

        var analysis = BreakoutCalculator.Analyze(candles, volumeThresholdMultiplier: 1.5m);

        analysis.Should().NotBeNull();
        analysis!.Is20PeriodHighBreakout.Should().BeTrue();
        analysis.Resistance20.Should().Be(100m); // Strictly reflects the prior 20 candles!
        analysis.IsVolumeConfirmed.Should().BeTrue(); // 50,000 > 10,000 * 1.5
    }

    [Fact]
    public void Analyze_ResistanceCalculation_NeverIncludesCurrentCandle()
    {
        // Prior 20 candles with High = 80
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
        {
            candles.Add(CreateCandle(high: 80m, low: 70m, close: 75m, volume: 10000, dayOffset: i));
        }

        // Current candle spikes to High = 150, but Close = 79
        candles.Add(CreateCandle(high: 150m, low: 75m, close: 79m, volume: 10000, dayOffset: 20));

        var analysis = BreakoutCalculator.Analyze(candles);

        analysis.Should().NotBeNull();
        // Crucial test for NO look-ahead bias: Resistance must be 80, NOT 150!
        analysis!.Resistance20.Should().Be(80m);
        // And since Close is 79, which is <= 80, breakout should be false!
        analysis.Is20PeriodHighBreakout.Should().BeFalse();
    }

    [Fact]
    public void Analyze_20PeriodLowBreakdown_DetectsWhenCloseFallsBelowPrior20Lows()
    {
        // Prior 20 candles with Lows between 50 and 60
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
        {
            candles.Add(CreateCandle(high: 65m, low: 50m, close: 55m, volume: 10000, dayOffset: i));
        }

        // Current candle breaks down: Low = 42, Close = 45
        candles.Add(CreateCandle(high: 52m, low: 42m, close: 45m, volume: 10000, dayOffset: 20));

        var analysis = BreakoutCalculator.Analyze(candles);

        analysis.Should().NotBeNull();
        analysis!.Support20.Should().Be(50m);
        analysis.Is20PeriodLowBreakdown.Should().BeTrue();
    }
}
