using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Domain.Calculators;

/// <summary>
/// Technical breakout and breakdown detector across 20-period and 50-period historical ranges.
/// Strictly eliminates look-ahead bias by evaluating historical resistance/support strictly on completed prior candles.
/// </summary>
public static class BreakoutCalculator
{
    /// <summary>
    /// Analyzes the latest candle for resistance breakout or support breakdown against prior historical candles.
    /// </summary>
    /// <param name="candles">Chronological series of candles (oldest first, current latest at index [^1]).</param>
    /// <param name="volumeThresholdMultiplier">Volume multiplier required to confirm breakout (default 1.25x average volume).</param>
    public static BreakoutAnalysis? Analyze(IReadOnlyList<Candle> candles, decimal volumeThresholdMultiplier = 1.25m)
    {
        if (candles == null || candles.Count < 21) // Need at least 20 prior candles + 1 current candle
            return null;

        var current = candles[^1];

        // 20-period historical range: candles [^21 .. ^2] (exactly 20 previous candles, excluding current candle [^1])
        int lookback20 = Math.Min(20, candles.Count - 1);
        int startIndex20 = candles.Count - 1 - lookback20;
        var window20 = candles.Skip(startIndex20).Take(lookback20).ToList();

        decimal resistance20 = window20.Max(c => c.High);
        decimal support20 = window20.Min(c => c.Low);

        bool is20HighBreakout = current.Close > resistance20;
        bool is20LowBreakdown = current.Close < support20;

        // 50-period historical range: candles [^51 .. ^2]
        decimal resistance50 = resistance20;
        decimal support50 = support20;
        bool is50HighBreakout = false;
        bool is50LowBreakdown = false;

        if (candles.Count >= 51)
        {
            int lookback50 = Math.Min(50, candles.Count - 1);
            int startIndex50 = candles.Count - 1 - lookback50;
            var window50 = candles.Skip(startIndex50).Take(lookback50).ToList();

            resistance50 = window50.Max(c => c.High);
            support50 = window50.Min(c => c.Low);

            is50HighBreakout = current.Close > resistance50;
            is50LowBreakdown = current.Close < support50;
        }

        // Volume confirmation calculation: 20-period average volume of prior candles
        decimal avgVol20 = (decimal)window20.Average(c => c.Volume);
        bool volumeConfirmed = (is20HighBreakout || is50HighBreakout) &&
                               avgVol20 > 0 &&
                               current.Volume >= (long)(avgVol20 * volumeThresholdMultiplier);

        return new BreakoutAnalysis
        {
            Is20PeriodHighBreakout = is20HighBreakout,
            Is50PeriodHighBreakout = is50HighBreakout,
            Is20PeriodLowBreakdown = is20LowBreakdown,
            Is50PeriodLowBreakdown = is50LowBreakdown,
            Resistance20 = Math.Round(resistance20, 2),
            Resistance50 = Math.Round(resistance50, 2),
            Support20 = Math.Round(support20, 2),
            Support50 = Math.Round(support50, 2),
            IsVolumeConfirmed = volumeConfirmed
        };
    }
}
