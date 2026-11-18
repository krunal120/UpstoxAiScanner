using FluentAssertions;
using UpstoxAiScanner.Domain.Calculators;
using UpstoxAiScanner.Domain.Models;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Indicators;

public class VolumeCalculatorTests
{
    private static Candle CreateCandle(long volume, int dayOffset)
    {
        var dt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOffset);
        return new Candle
        {
            Timestamp = dt,
            Open = 100m,
            High = 105m,
            Low = 95m,
            Close = 102m,
            Volume = volume,
            OpenInterest = 0
        };
    }

    [Fact]
    public void Analyze_VolumeSurge_DetectsWhenVolumeExceedsThresholdRatio()
    {
        // 20 candles with 10,000 baseline volume
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
        {
            candles.Add(CreateCandle(volume: 10000, dayOffset: i));
        }

        // 21st candle has 25,000 volume (2.5x baseline)
        candles.Add(CreateCandle(volume: 25000, dayOffset: 20));

        var analysis = VolumeCalculator.Analyze(candles, surgeThreshold: 1.5m);

        analysis.Should().NotBeNull();
        analysis!.CurrentVolume.Should().Be(25000);
        analysis.AverageVolume20.Should().Be(10000);
        analysis.VolumeRatio.Should().Be(2.5m);
        analysis.IsVolumeSurge.Should().BeTrue();
    }

    [Fact]
    public void Analyze_NormalVolume_DoesNotTriggerSurge()
    {
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
        {
            candles.Add(CreateCandle(volume: 10000, dayOffset: i));
        }

        // 21st candle has 11,000 volume (1.1x)
        candles.Add(CreateCandle(volume: 11000, dayOffset: 20));

        var analysis = VolumeCalculator.Analyze(candles, surgeThreshold: 1.5m);

        analysis.Should().NotBeNull();
        analysis!.VolumeRatio.Should().Be(1.1m);
        analysis.IsVolumeSurge.Should().BeFalse();
    }
}
