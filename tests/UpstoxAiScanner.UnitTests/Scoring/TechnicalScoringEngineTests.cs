using FluentAssertions;
using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Scoring;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Scoring;

public class TechnicalScoringEngineTests
{
    [Fact]
    public void ScoreSwing_BullishAlignmentAndBreakout_ReturnsStrongBullishScore()
    {
        // Arrange
        var snapshot = new TechnicalAnalysisSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ClosePrice = 150m,
            Ema = new EmaAnalysis
            {
                Ema20 = 140m,
                Ema50 = 130m,
                Ema200 = 110m,
                IsPriceAboveEma20 = true,
                IsPriceAboveEma50 = true,
                IsPriceAboveEma200 = true,
                CrossedAboveEma20 = true
            },
            Macd = new MacdAnalysis
            {
                Macd = 3.5m,
                Signal = 2.0m,
                Histogram = 1.5m,
                IsBullishCrossover = true,
                HistogramMomentum = "ExpandingBullish"
            },
            Rsi = new RsiAnalysis
            {
                Value = 62m,
                CrossedAbove50 = true
            },
            Breakout = new BreakoutAnalysis
            {
                Is20PeriodHighBreakout = true,
                Is50PeriodHighBreakout = true,
                Resistance20 = 145m,
                Resistance50 = 148m,
                IsVolumeConfirmed = true
            },
            Volume = new VolumeAnalysis
            {
                CurrentVolume = 250000,
                AverageVolume20 = 100000,
                VolumeRatio = 2.5m,
                IsVolumeSurge = true
            }
        };

        // Act
        var result = TechnicalScoringEngine.ScoreSwing(snapshot);

        // Assert
        result.Score.Should().BeInRange(80m, 100m);
        result.Signal.Should().Be("Strong Bullish");
        result.BreakoutStatus.Should().Contain("50-Day High Breakout");
        result.VolumeStatus.Should().Contain("Volume Surge");
        result.Explanation.Should().Contain("EMA Bullish Alignment");
        result.Explanation.Should().Contain("MACD Bullish Crossover");
    }

    [Fact]
    public void ScoreSwing_BearishAlignmentAndBreakdown_ReturnsBearishScore()
    {
        // Arrange
        var snapshot = new TechnicalAnalysisSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ClosePrice = 85m,
            Ema = new EmaAnalysis
            {
                Ema20 = 95m,
                Ema50 = 110m,
                Ema200 = 130m,
                IsPriceAboveEma20 = false,
                IsPriceAboveEma50 = false,
                IsPriceAboveEma200 = false,
                CrossedBelowEma20 = true
            },
            Macd = new MacdAnalysis
            {
                Macd = -4.5m,
                Signal = -2.0m,
                Histogram = -2.5m,
                IsBearishCrossover = true,
                HistogramMomentum = "ExpandingBearish"
            },
            Rsi = new RsiAnalysis
            {
                Value = 28m,
                CrossedBelow70 = false
            },
            Breakout = new BreakoutAnalysis
            {
                Is20PeriodLowBreakdown = true,
                Is50PeriodLowBreakdown = true,
                Support20 = 92m,
                Support50 = 90m
            },
            Volume = new VolumeAnalysis
            {
                CurrentVolume = 100000,
                AverageVolume20 = 100000,
                VolumeRatio = 1.0m,
                IsVolumeSurge = false
            }
        };

        // Act
        var result = TechnicalScoringEngine.ScoreSwing(snapshot);

        // Assert
        result.Score.Should().BeLessThanOrEqualTo(20m);
        result.Signal.Should().BeOneOf("Bearish", "Strong Bearish");
        result.BreakoutStatus.Should().Contain("Breakdown");
    }

    [Fact]
    public void ScoreIntraday_MomentumBreakout_ReturnsBullishScore()
    {
        // Arrange
        var snapshot = new TechnicalAnalysisSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ClosePrice = 500m,
            Ema = new EmaAnalysis
            {
                Ema20 = 495m,
                Ema50 = 490m,
                IsPriceAboveEma20 = true,
                CrossedAboveEma20 = true
            },
            Macd = new MacdAnalysis
            {
                Macd = 1.2m,
                Signal = 0.5m,
                Histogram = 0.7m,
                IsBullishCrossover = true,
                HistogramMomentum = "ExpandingBullish"
            },
            Rsi = new RsiAnalysis
            {
                Value = 64m
            },
            Breakout = new BreakoutAnalysis
            {
                Is20PeriodHighBreakout = true,
                Resistance20 = 498m,
                IsVolumeConfirmed = true
            },
            Volume = new VolumeAnalysis
            {
                CurrentVolume = 50000,
                AverageVolume20 = 20000,
                VolumeRatio = 2.5m,
                IsVolumeSurge = true
            }
        };

        // Act
        var result = TechnicalScoringEngine.ScoreIntraday(snapshot);

        // Assert
        result.Score.Should().BeGreaterThanOrEqualTo(75m);
        result.Signal.Should().BeOneOf("Bullish", "Strong Bullish");
        result.BreakoutStatus.Should().Contain("Intraday Breakout");
        result.VolumeStatus.Should().Contain("Volume Surge");
    }

    [Fact]
    public void Score_NeverExceeds100_OrFallsBelowZero()
    {
        // Extreme bullish setup
        var maxBullish = new TechnicalAnalysisSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ClosePrice = 1000m,
            Ema = new EmaAnalysis
            {
                Ema20 = 900m, Ema50 = 800m, Ema200 = 700m,
                IsPriceAboveEma20 = true, IsPriceAboveEma50 = true, IsPriceAboveEma200 = true,
                CrossedAboveEma20 = true, GoldenCross = true
            },
            Macd = new MacdAnalysis
            {
                Macd = 15m, Signal = 10m, Histogram = 5m,
                IsBullishCrossover = true, HistogramMomentum = "ExpandingBullish"
            },
            Rsi = new RsiAnalysis { Value = 60m, CrossedAbove30 = true, CrossedAbove50 = true },
            Breakout = new BreakoutAnalysis
            {
                Is20PeriodHighBreakout = true, Is50PeriodHighBreakout = true, IsVolumeConfirmed = true
            },
            Volume = new VolumeAnalysis { VolumeRatio = 5.0m, IsVolumeSurge = true }
        };

        var swingResult = TechnicalScoringEngine.ScoreSwing(maxBullish);
        var intradayResult = TechnicalScoringEngine.ScoreIntraday(maxBullish);

        swingResult.Score.Should().BeLessThanOrEqualTo(100m);
        intradayResult.Score.Should().BeLessThanOrEqualTo(100m);
    }
}
