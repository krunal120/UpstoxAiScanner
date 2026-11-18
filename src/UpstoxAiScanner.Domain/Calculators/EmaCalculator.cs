using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Domain.Calculators;

/// <summary>
/// Exponential Moving Average (EMA) calculator and trend alignment analyzer.
/// </summary>
public static class EmaCalculator
{
    /// <summary>
    /// Calculates EMA series for a chronological series of price values.
    /// Returns a list of nullable decimals with the same length as the input values.
    /// Values before (period - 1) will be null.
    /// </summary>
    public static List<decimal?> CalculateSeries(IReadOnlyList<decimal> values, int period)
    {
        var result = new List<decimal?>(new decimal?[values.Count]);

        if (values == null || values.Count < period || period <= 0)
            return result;

        // First EMA value is SMA of the first 'period' values
        decimal sum = 0m;
        for (int i = 0; i < period; i++)
        {
            sum += values[i];
        }

        decimal prevEma = sum / period;
        result[period - 1] = prevEma;

        decimal multiplier = 2m / (period + 1m);

        // Subsequent EMAs: (Price - prevEMA) * multiplier + prevEMA
        for (int i = period; i < values.Count; i++)
        {
            decimal currentEma = (values[i] - prevEma) * multiplier + prevEma;
            result[i] = currentEma;
            prevEma = currentEma;
        }

        return result;
    }

    /// <summary>
    /// Overload to calculate EMA series from a candle collection using Close prices.
    /// </summary>
    public static List<decimal?> CalculateSeries(IReadOnlyList<Candle> candles, int period)
    {
        var closePrices = candles.Select(c => c.Close).ToList();
        return CalculateSeries(closePrices, period);
    }

    /// <summary>
    /// Analyzes EMA 20, EMA 50, and EMA 200 for alignment, price position, and crossover events.
    /// </summary>
    public static EmaAnalysis? Analyze(IReadOnlyList<Candle> candles)
    {
        if (candles == null || candles.Count < 20)
            return null;

        var closePrices = candles.Select(c => c.Close).ToList();

        var ema20Series = CalculateSeries(closePrices, 20);
        var ema50Series = CalculateSeries(closePrices, 50);
        var ema200Series = CalculateSeries(closePrices, 200);

        decimal currentClose = closePrices[^1];
        decimal prevClose = closePrices.Count >= 2 ? closePrices[^2] : currentClose;

        decimal? currentEma20 = ema20Series[^1];
        decimal? prevEma20 = ema20Series.Count >= 2 ? ema20Series[^2] : null;

        decimal? currentEma50 = ema50Series[^1];
        decimal? prevEma50 = ema50Series.Count >= 2 ? ema50Series[^2] : null;

        decimal? currentEma200 = ema200Series[^1];
        decimal? prevEma200 = ema200Series.Count >= 2 ? ema200Series[^2] : null;

        bool isPriceAbove20 = currentEma20.HasValue && currentClose > currentEma20.Value;
        bool isPriceAbove50 = currentEma50.HasValue && currentClose > currentEma50.Value;
        bool isPriceAbove200 = currentEma200.HasValue && currentClose > currentEma200.Value;

        bool crossedAbove20 = prevEma20.HasValue && currentEma20.HasValue && prevClose <= prevEma20.Value && currentClose > currentEma20.Value;
        bool crossedBelow20 = prevEma20.HasValue && currentEma20.HasValue && prevClose >= prevEma20.Value && currentClose < currentEma20.Value;

        bool goldenCross = prevEma50.HasValue && prevEma200.HasValue && currentEma50.HasValue && currentEma200.HasValue &&
                           prevEma50.Value <= prevEma200.Value && currentEma50.Value > currentEma200.Value;

        bool deathCross = prevEma50.HasValue && prevEma200.HasValue && currentEma50.HasValue && currentEma200.HasValue &&
                          prevEma50.Value >= prevEma200.Value && currentEma50.Value < currentEma200.Value;

        return new EmaAnalysis
        {
            Ema20 = currentEma20.HasValue ? Math.Round(currentEma20.Value, 2) : null,
            Ema50 = currentEma50.HasValue ? Math.Round(currentEma50.Value, 2) : null,
            Ema200 = currentEma200.HasValue ? Math.Round(currentEma200.Value, 2) : null,
            IsPriceAboveEma20 = isPriceAbove20,
            IsPriceAboveEma50 = isPriceAbove50,
            IsPriceAboveEma200 = isPriceAbove200,
            CrossedAboveEma20 = crossedAbove20,
            CrossedBelowEma20 = crossedBelow20,
            GoldenCross = goldenCross,
            DeathCross = deathCross
        };
    }
}
