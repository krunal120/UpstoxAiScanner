using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Domain.Calculators;

/// <summary>
/// Relative Strength Index (RSI) calculation using Wilder's Smoothing method.
/// </summary>
public static class RsiCalculator
{
    /// <summary>
    /// Calculates RSI series for a chronological list of candles.
    /// Returns a list of nullable decimals with the same length as the input candles.
    /// Values before (period + 1) will be null.
    /// </summary>
    public static List<decimal?> CalculateSeries(IReadOnlyList<Candle> candles, int period = 14)
    {
        var result = new List<decimal?>(new decimal?[candles.Count]);

        if (candles == null || candles.Count <= period)
            return result;

        decimal sumGain = 0m;
        decimal sumLoss = 0m;

        // Calculate initial SMA of gains and losses for the first 'period' changes
        for (int i = 1; i <= period; i++)
        {
            decimal change = candles[i].Close - candles[i - 1].Close;
            if (change > 0)
                sumGain += change;
            else if (change < 0)
                sumLoss += Math.Abs(change);
        }

        decimal avgGain = sumGain / period;
        decimal avgLoss = sumLoss / period;

        result[period] = ComputeRsi(avgGain, avgLoss);

        // Subsequent periods use Wilder's smoothing: ((prevAvg * (period - 1)) + current) / period
        for (int i = period + 1; i < candles.Count; i++)
        {
            decimal change = candles[i].Close - candles[i - 1].Close;
            decimal currentGain = change > 0 ? change : 0m;
            decimal currentLoss = change < 0 ? Math.Abs(change) : 0m;

            avgGain = ((avgGain * (period - 1)) + currentGain) / period;
            avgLoss = ((avgLoss * (period - 1)) + currentLoss) / period;

            result[i] = ComputeRsi(avgGain, avgLoss);
        }

        return result;
    }

    /// <summary>
    /// Computes latest RSI analysis including overbought/oversold status and crossover signals.
    /// </summary>
    public static RsiAnalysis? Analyze(IReadOnlyList<Candle> candles, int period = 14)
    {
        var series = CalculateSeries(candles, period);
        if (series.Count == 0 || !series[^1].HasValue)
            return null;

        decimal current = series[^1]!.Value;
        decimal? previous = series.Count >= 2 ? series[^2] : null;

        bool crossedAbove30 = previous.HasValue && previous.Value <= 30m && current > 30m;
        bool crossedBelow70 = previous.HasValue && previous.Value >= 70m && current < 70m;
        bool crossedAbove50 = previous.HasValue && previous.Value <= 50m && current > 50m;
        bool crossedBelow50 = previous.HasValue && previous.Value >= 50m && current < 50m;

        return new RsiAnalysis
        {
            Value = Math.Round(current, 2),
            CrossedAbove30 = crossedAbove30,
            CrossedBelow70 = crossedBelow70,
            CrossedAbove50 = crossedAbove50,
            CrossedBelow50 = crossedBelow50
        };
    }

    private static decimal ComputeRsi(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0m)
            return avgGain == 0m ? 50m : 100m;

        decimal rs = avgGain / avgLoss;
        decimal rsi = 100m - (100m / (1m + rs));
        return Math.Clamp(rsi, 0m, 100m);
    }
}
