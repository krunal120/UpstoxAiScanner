using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Domain.Calculators;

/// <summary>
/// Moving Average Convergence Divergence (MACD) calculator and momentum analyzer.
/// </summary>
public static class MacdCalculator
{
    public record MacdPoint(decimal? Macd, decimal? Signal, decimal? Histogram);

    /// <summary>
    /// Calculates full MACD series (MACD line, Signal line, Histogram) for candle list.
    /// </summary>
    public static List<MacdPoint> CalculateSeries(
        IReadOnlyList<Candle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        if (candles == null || candles.Count < slowPeriod)
        {
            return candles != null 
                ? candles.Select(_ => new MacdPoint(null, null, null)).ToList() 
                : new List<MacdPoint>();
        }

        var result = new List<MacdPoint>(new MacdPoint[candles.Count]);

        var closePrices = candles.Select(c => c.Close).ToList();
        var fastEma = EmaCalculator.CalculateSeries(closePrices, fastPeriod);
        var slowEma = EmaCalculator.CalculateSeries(closePrices, slowPeriod);

        // Compute MACD Line = Fast EMA - Slow EMA
        var macdLine = new List<decimal?>(new decimal?[candles.Count]);
        for (int i = 0; i < candles.Count; i++)
        {
            if (fastEma[i].HasValue && slowEma[i].HasValue)
            {
                macdLine[i] = fastEma[i]!.Value - slowEma[i]!.Value;
            }
        }

        // Compute Signal Line = EMA(MACD Line, signalPeriod)
        // Find first index where MACD Line has a non-null value (usually slowPeriod - 1)
        int firstMacdIndex = macdLine.FindIndex(m => m.HasValue);
        if (firstMacdIndex == -1 || candles.Count - firstMacdIndex < signalPeriod)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                result[i] = new MacdPoint(macdLine[i], null, null);
            }
            return result;
        }

        // Extract valid MACD line segment for EMA calculation
        var validMacdValues = macdLine.Skip(firstMacdIndex).Select(m => m!.Value).ToList();
        var signalSegment = EmaCalculator.CalculateSeries(validMacdValues, signalPeriod);

        for (int i = 0; i < candles.Count; i++)
        {
            if (i < firstMacdIndex)
            {
                result[i] = new MacdPoint(null, null, null);
            }
            else
            {
                int segIndex = i - firstMacdIndex;
                decimal? m = macdLine[i];
                decimal? s = signalSegment[segIndex];
                decimal? h = (m.HasValue && s.HasValue) ? (m.Value - s.Value) : null;
                result[i] = new MacdPoint(m, s, h);
            }
        }

        return result;
    }

    /// <summary>
    /// Analyzes the latest candle for MACD line, signal line, histogram, crossovers, and momentum trend.
    /// </summary>
    public static MacdAnalysis? Analyze(
        IReadOnlyList<Candle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        var series = CalculateSeries(candles, fastPeriod, slowPeriod, signalPeriod);
        if (series.Count == 0 || !series[^1].Macd.HasValue || !series[^1].Signal.HasValue || !series[^1].Histogram.HasValue)
            return null;

        var current = series[^1];
        var previous = series.Count >= 2 ? series[^2] : null;

        decimal currentMacd = current.Macd!.Value;
        decimal currentSignal = current.Signal!.Value;
        decimal currentHist = current.Histogram!.Value;

        bool isBullishCross = false;
        bool isBearishCross = false;
        string momentum = "Neutral";

        if (previous != null && previous.Macd.HasValue && previous.Signal.HasValue && previous.Histogram.HasValue)
        {
            decimal prevMacd = previous.Macd.Value;
            decimal prevSignal = previous.Signal.Value;
            decimal prevHist = previous.Histogram.Value;

            isBullishCross = prevMacd <= prevSignal && currentMacd > currentSignal;
            isBearishCross = prevMacd >= prevSignal && currentMacd < currentSignal;

            if (currentHist > 0)
            {
                momentum = currentHist > prevHist ? "ExpandingBullish" : "ContractingBullish";
            }
            else if (currentHist < 0)
            {
                momentum = currentHist < prevHist ? "ExpandingBearish" : "ContractingBearish";
            }
        }
        else
        {
            if (currentHist > 0) momentum = "ExpandingBullish";
            else if (currentHist < 0) momentum = "ExpandingBearish";
        }

        return new MacdAnalysis
        {
            Macd = Math.Round(currentMacd, 4),
            Signal = Math.Round(currentSignal, 4),
            Histogram = Math.Round(currentHist, 4),
            IsBullishCrossover = isBullishCross,
            IsBearishCrossover = isBearishCross,
            HistogramMomentum = momentum
        };
    }
}
