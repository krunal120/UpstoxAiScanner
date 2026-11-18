using FluentAssertions;
using UpstoxAiScanner.Domain.Calculators;
using UpstoxAiScanner.Domain.Models;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Indicators;

public class MacdCalculatorTests
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
            Volume = 100000,
            OpenInterest = 0
        };
    }

    [Fact]
    public void CalculateSeries_InsufficientCandles_ReturnsEmptyOrNulls()
    {
        var candles = Enumerable.Range(0, 15).Select(i => CreateCandle(100 + i, i)).ToList();
        var series = MacdCalculator.CalculateSeries(candles);

        series.Should().HaveCount(15);
        series.Should().AllSatisfy(p => p.Macd.Should().BeNull());
    }

    [Fact]
    public void Analyze_WithSufficientData_CalculatesMacdSignalAndHistogram()
    {
        // 60 candles with accelerating trend to produce strong momentum
        var candles = Enumerable.Range(0, 60)
            .Select(i => CreateCandle(100m + (decimal)Math.Pow(i, 1.3), i))
            .ToList();

        var analysis = MacdCalculator.Analyze(candles);

        analysis.Should().NotBeNull();
        analysis!.Macd.Should().BeGreaterThan(0m);
        analysis.Signal.Should().BeGreaterThan(0m);
        analysis.IsAboveZero.Should().BeTrue();
        analysis.IsBelowZero.Should().BeFalse();
        analysis.HistogramMomentum.Should().NotBe("Neutral");
    }

    [Fact]
    public void Analyze_BullishCrossover_DetectsWhenMacdCrossesAboveSignal()
    {
        // Downward trend followed by sharp reversal upward to produce a bullish cross
        var prices = new List<decimal>();
        for (int i = 0; i < 40; i++) prices.Add(200m - (i * 2m)); // downtrend
        for (int i = 0; i < 15; i++) prices.Add(120m + (i * 6m)); // strong upward reversal

        var candles = prices.Select((p, idx) => CreateCandle(p, idx)).ToList();
        var analysis = MacdCalculator.Analyze(candles);

        analysis.Should().NotBeNull();
        analysis!.Macd.Should().BeGreaterThan(analysis.Signal);
    }
}
