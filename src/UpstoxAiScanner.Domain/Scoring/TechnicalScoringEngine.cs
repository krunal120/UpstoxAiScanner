using UpstoxAiScanner.Domain.Indicators;

namespace UpstoxAiScanner.Domain.Scoring;

/// <summary>
/// Scoring output containing the final score, signal classification, and transparent rule-based explanation.
/// </summary>
public record ScanScoreResult
{
    public decimal Score { get; init; }
    public string Signal { get; init; } = "Neutral";
    public string BreakoutStatus { get; init; } = "None";
    public string VolumeStatus { get; init; } = "Normal";
    public string Explanation { get; init; } = string.Empty;
}

/// <summary>
/// Rule-based technical scoring engine assigning a score from 0 to 100 for Swing and Intraday setups.
/// </summary>
public static class TechnicalScoringEngine
{
    /// <summary>
    /// Scores a daily swing trading setup using EMA alignment, MACD momentum, Wilder's RSI, and Breakouts.
    /// </summary>
    public static ScanScoreResult ScoreSwing(TechnicalAnalysisSnapshot snapshot)
    {
        decimal score = 50m; // Neutral starting baseline
        var reasons = new List<string>();
        string breakoutStatus = "None";
        string volumeStatus = "Normal";

        // 1. Trend & EMA Alignment (Up to +/- 30 points)
        if (snapshot.Ema != null)
        {
            if (snapshot.Ema.IsBullishAlignment)
            {
                score += 25m;
                reasons.Add("EMA Bullish Alignment (EMA20 > EMA50 > EMA200) (+25)");
            }
            else if (snapshot.Ema.IsBearishAlignment)
            {
                score -= 25m;
                reasons.Add("EMA Bearish Alignment (EMA20 < EMA50 < EMA200) (-25)");
            }
            else if (snapshot.Ema.IsPriceAboveEma20 && snapshot.Ema.IsPriceAboveEma50)
            {
                score += 15m;
                reasons.Add("Price above EMA20 and EMA50 (+15)");
            }

            if (snapshot.Ema.IsPriceAboveEma200)
            {
                score += 10m;
                reasons.Add("Price above 200 EMA (Long-term Bull Market) (+10)");
            }
            else if (snapshot.Ema.Ema200.HasValue && !snapshot.Ema.IsPriceAboveEma200)
            {
                score -= 10m;
                reasons.Add("Price below 200 EMA (Long-term Downtrend) (-10)");
            }

            if (snapshot.Ema.GoldenCross)
            {
                score += 10m;
                reasons.Add("Golden Cross (EMA50 crossed above EMA200) (+10)");
            }
            else if (snapshot.Ema.DeathCross)
            {
                score -= 10m;
                reasons.Add("Death Cross (EMA50 crossed below EMA200) (-10)");
            }

            if (snapshot.Ema.CrossedAboveEma20)
            {
                score += 10m;
                reasons.Add("Price crossed above EMA20 (+10)");
            }
            else if (snapshot.Ema.CrossedBelowEma20)
            {
                score -= 10m;
                reasons.Add("Price crossed below EMA20 (-10)");
            }
        }

        // 2. MACD Momentum (Up to +/- 25 points)
        if (snapshot.Macd != null)
        {
            if (snapshot.Macd.IsBullishCrossover)
            {
                score += 20m;
                reasons.Add("MACD Bullish Crossover (+20)");
            }
            else if (snapshot.Macd.IsBearishCrossover)
            {
                score -= 20m;
                reasons.Add("MACD Bearish Crossover (-20)");
            }

            if (snapshot.Macd.IsAboveZero)
            {
                score += 8m;
                reasons.Add("MACD above zero line (+8)");
            }
            else
            {
                score -= 8m;
                reasons.Add("MACD below zero line (-8)");
            }

            if (snapshot.Macd.HistogramMomentum == "ExpandingBullish")
            {
                score += 7m;
                reasons.Add("Expanding Bullish MACD Histogram (+7)");
            }
            else if (snapshot.Macd.HistogramMomentum == "ExpandingBearish")
            {
                score -= 7m;
                reasons.Add("Expanding Bearish MACD Histogram (-7)");
            }
        }

        // 3. RSI Conditions (Up to +/- 20 points)
        if (snapshot.Rsi != null)
        {
            if (snapshot.Rsi.CrossedAbove30)
            {
                score += 20m;
                reasons.Add("RSI Oversold Recovery (Crossed above 30) (+20)");
            }
            else if (snapshot.Rsi.CrossedBelow70)
            {
                score -= 20m;
                reasons.Add("RSI Overbought Pullback (Crossed below 70) (-20)");
            }
            else if (snapshot.Rsi.Value >= 50m && snapshot.Rsi.Value <= 65m)
            {
                score += 12m;
                reasons.Add($"RSI in Bullish Momentum Zone ({snapshot.Rsi.Value:F1}) (+12)");
            }
            else if (snapshot.Rsi.IsOverbought)
            {
                score -= 8m;
                reasons.Add($"RSI Overbought ({snapshot.Rsi.Value:F1} >= 70) (-8)");
            }
            else if (snapshot.Rsi.IsOversold)
            {
                score -= 8m;
                reasons.Add($"RSI Oversold ({snapshot.Rsi.Value:F1} <= 30) (-8)");
            }

            if (snapshot.Rsi.CrossedAbove50)
            {
                score += 8m;
                reasons.Add("RSI crossed above 50 (+8)");
            }
        }

        // 4. Breakout & Volume Analysis (Up to +/- 25 points)
        if (snapshot.Breakout != null)
        {
            if (snapshot.Breakout.Is50PeriodHighBreakout && snapshot.Breakout.IsVolumeConfirmed)
            {
                score += 25m;
                breakoutStatus = "50-Day High Breakout (Volume Confirmed)";
                reasons.Add("50-Day High Breakout with Volume Confirmation (+25)");
            }
            else if (snapshot.Breakout.Is20PeriodHighBreakout && snapshot.Breakout.IsVolumeConfirmed)
            {
                score += 20m;
                breakoutStatus = "20-Day High Breakout (Volume Confirmed)";
                reasons.Add("20-Day High Breakout with Volume Confirmation (+20)");
            }
            else if (snapshot.Breakout.Is20PeriodHighBreakout)
            {
                score += 12m;
                breakoutStatus = "20-Day High Breakout";
                reasons.Add("20-Day High Breakout (+12)");
            }
            else if (snapshot.Breakout.Is50PeriodLowBreakdown)
            {
                score -= 25m;
                breakoutStatus = "50-Day Low Breakdown";
                reasons.Add("50-Day Low Breakdown (-25)");
            }
            else if (snapshot.Breakout.Is20PeriodLowBreakdown)
            {
                score -= 20m;
                breakoutStatus = "20-Day Low Breakdown";
                reasons.Add("20-Day Low Breakdown (-20)");
            }
        }

        if (snapshot.Volume != null)
        {
            if (snapshot.Volume.IsVolumeSurge)
            {
                score += 10m;
                volumeStatus = $"Volume Surge ({snapshot.Volume.VolumeRatio:F1}x)";
                reasons.Add($"Volume Surge ({snapshot.Volume.VolumeRatio:F1}x average) (+10)");
            }
            else if (snapshot.Volume.VolumeRatio >= 1.2m)
            {
                volumeStatus = "Above Average";
                score += 5m;
                reasons.Add($"Above Average Volume ({snapshot.Volume.VolumeRatio:F1}x) (+5)");
            }
        }

        // 5. Price Action Quality & Candle Spread Confluence
        if (snapshot.OpenPrice.HasValue && snapshot.HighPrice.HasValue && snapshot.LowPrice.HasValue)
        {
            decimal candleRange = snapshot.HighPrice.Value - snapshot.LowPrice.Value;
            if (candleRange > 0)
            {
                decimal upperWick = snapshot.HighPrice.Value - Math.Max(snapshot.OpenPrice.Value, snapshot.ClosePrice);
                decimal lowerWick = Math.Min(snapshot.OpenPrice.Value, snapshot.ClosePrice) - snapshot.LowPrice.Value;

                // Rejection Wick: Upper wick is more than 50% of candle range (sellers knocked price down)
                if (upperWick > candleRange * 0.5m)
                {
                    score -= 10m;
                    reasons.Add("Upper Rejection Wick (Supply Overhead) (-10)");
                }
                // Strong Close: Green candle closing in top 15% of range
                else if (snapshot.ClosePrice > snapshot.OpenPrice.Value && (snapshot.HighPrice.Value - snapshot.ClosePrice) <= candleRange * 0.15m)
                {
                    score += 8m;
                    reasons.Add("Strong Close near High (+8)");
                }

                // Bullish Pinbar / Hammer: Lower wick > 50% of range
                if (lowerWick > candleRange * 0.5m && snapshot.ClosePrice >= snapshot.OpenPrice.Value)
                {
                    score += 8m;
                    reasons.Add("Bullish Price Rejection of Lows (+8)");
                }
            }
        }

        // 6. Mean-Reversion Overextension Guard (Avoid buying top of rubber-band stretch)
        if (snapshot.Ema?.Ema20.HasValue == true && snapshot.Ema.Ema20.Value > 0)
        {
            decimal distFromEma20 = (snapshot.ClosePrice - snapshot.Ema.Ema20.Value) / snapshot.Ema.Ema20.Value;
            if (distFromEma20 > 0.12m)
            {
                score -= 10m;
                reasons.Add($"Overextended from 20 EMA ({distFromEma20 * 100:F1}% stretch, Pullback Risk) (-10)");
            }
        }

        // Clamp final score strictly between 0 and 100
        score = Math.Clamp(score, 0m, 100m);
        string signal = CategorizeSignal(score);

        return new ScanScoreResult
        {
            Score = Math.Round(score, 1),
            Signal = signal,
            BreakoutStatus = breakoutStatus,
            VolumeStatus = volumeStatus,
            Explanation = string.Join("; ", reasons)
        };
    }

    /// <summary>
    /// Scores a 5-minute intraday setup using intraday momentum, MACD crossover, fast EMA trend, and volume surges.
    /// </summary>
    public static ScanScoreResult ScoreIntraday(TechnicalAnalysisSnapshot snapshot)
    {
        decimal score = 50m; // Neutral starting baseline
        var reasons = new List<string>();
        string breakoutStatus = "None";
        string volumeStatus = "Normal";

        // 1. Intraday Trend & Fast EMA (Up to +/- 30 points)
        if (snapshot.Ema != null)
        {
            if (snapshot.Ema.Ema20.HasValue && snapshot.Ema.Ema50.HasValue)
            {
                if (snapshot.Ema.Ema20.Value > snapshot.Ema.Ema50.Value)
                {
                    score += 15m;
                    reasons.Add("Intraday Trend: EMA20 > EMA50 (+15)");
                }
                else
                {
                    score -= 15m;
                    reasons.Add("Intraday Trend: EMA20 < EMA50 (-15)");
                }
            }

            if (snapshot.Ema.CrossedAboveEma20)
            {
                score += 15m;
                reasons.Add("Price crossed above EMA20 (+15)");
            }
            else if (snapshot.Ema.CrossedBelowEma20)
            {
                score -= 15m;
                reasons.Add("Price crossed below EMA20 (-15)");
            }
            else if (snapshot.Ema.IsPriceAboveEma20)
            {
                score += 8m;
                reasons.Add("Price above EMA20 (+8)");
            }
            else
            {
                score -= 8m;
                reasons.Add("Price below EMA20 (-8)");
            }
        }

        // 2. Intraday MACD Momentum (Up to +/- 30 points)
        if (snapshot.Macd != null)
        {
            if (snapshot.Macd.IsBullishCrossover)
            {
                score += 22m;
                reasons.Add("Intraday MACD Bullish Crossover (+22)");
            }
            else if (snapshot.Macd.IsBearishCrossover)
            {
                score -= 22m;
                reasons.Add("Intraday MACD Bearish Crossover (-22)");
            }

            if (snapshot.Macd.IsAboveZero)
            {
                score += 8m;
                reasons.Add("MACD Line above zero (+8)");
            }
            else
            {
                score -= 8m;
                reasons.Add("MACD Line below zero (-8)");
            }

            if (snapshot.Macd.HistogramMomentum == "ExpandingBullish")
            {
                score += 8m;
                reasons.Add("Expanding Bullish Momentum (+8)");
            }
            else if (snapshot.Macd.HistogramMomentum == "ExpandingBearish")
            {
                score -= 8m;
                reasons.Add("Expanding Bearish Momentum (-8)");
            }
        }

        // 3. Intraday RSI (Up to +/- 20 points)
        if (snapshot.Rsi != null)
        {
            if (snapshot.Rsi.CrossedAbove30)
            {
                score += 20m;
                reasons.Add("Intraday Oversold Reversal (RSI crossed above 30) (+20)");
            }
            else if (snapshot.Rsi.CrossedBelow70)
            {
                score -= 20m;
                reasons.Add("Intraday Overbought Breakdown (RSI crossed below 70) (-20)");
            }
            else if (snapshot.Rsi.Value >= 55m && snapshot.Rsi.Value <= 70m)
            {
                score += 12m;
                reasons.Add($"Intraday Bullish RSI Zone ({snapshot.Rsi.Value:F1}) (+12)");
            }
            else if (snapshot.Rsi.IsOverbought)
            {
                score -= 10m;
                reasons.Add($"Intraday Overbought ({snapshot.Rsi.Value:F1}) (-10)");
            }
            else if (snapshot.Rsi.IsOversold)
            {
                score -= 10m;
                reasons.Add($"Intraday Oversold ({snapshot.Rsi.Value:F1}) (-10)");
            }
        }

        // 4. Intraday Breakout & Volume Surge (Up to +/- 25 points)
        if (snapshot.Breakout != null)
        {
            if (snapshot.Breakout.Is20PeriodHighBreakout && snapshot.Breakout.IsVolumeConfirmed)
            {
                score += 25m;
                breakoutStatus = "Intraday Breakout (Volume Confirmed)";
                reasons.Add("Intraday High Breakout with Volume Surge (+25)");
            }
            else if (snapshot.Breakout.Is20PeriodHighBreakout)
            {
                score += 15m;
                breakoutStatus = "Intraday Breakout";
                reasons.Add("Intraday High Breakout (+15)");
            }
            else if (snapshot.Breakout.Is20PeriodLowBreakdown)
            {
                score -= 20m;
                breakoutStatus = "Intraday Breakdown";
                reasons.Add("Intraday Low Breakdown (-20)");
            }
        }

        if (snapshot.Volume != null)
        {
            if (snapshot.Volume.VolumeRatio >= 2.0m)
            {
                score += 15m;
                volumeStatus = $"High Volume Surge ({snapshot.Volume.VolumeRatio:F1}x)";
                reasons.Add($"Major Volume Surge ({snapshot.Volume.VolumeRatio:F1}x) (+15)");
            }
            else if (snapshot.Volume.IsVolumeSurge)
            {
                score += 10m;
                volumeStatus = $"Volume Surge ({snapshot.Volume.VolumeRatio:F1}x)";
                reasons.Add($"Volume Surge ({snapshot.Volume.VolumeRatio:F1}x) (+10)");
            }
        }

        // 5. Multi-Timeframe Trend Confluence (MTF) - Align 5m with Daily Higher Timeframe
        if (snapshot.HigherTimeframeEma != null)
        {
            if (snapshot.HigherTimeframeEma.IsBullishAlignment ||
               (snapshot.HigherTimeframeEma.Ema20.HasValue && snapshot.HigherTimeframeEma.Ema50.HasValue && snapshot.HigherTimeframeEma.Ema20.Value > snapshot.HigherTimeframeEma.Ema50.Value))
            {
                score += 15m;
                reasons.Add("MTF Confluence: Daily Higher Timeframe Trend is Bullish (+15)");
            }
            else if (snapshot.HigherTimeframeEma.IsBearishAlignment ||
                    (snapshot.HigherTimeframeEma.Ema20.HasValue && snapshot.HigherTimeframeEma.Ema50.HasValue && snapshot.HigherTimeframeEma.Ema20.Value < snapshot.HigherTimeframeEma.Ema50.Value))
            {
                score -= 15m;
                reasons.Add("MTF Warning: Counter-Trend Setup against Bearish Daily Trend (-15)");
            }
        }

        // 6. Intraday Price Action Quality & Rejection Analysis
        if (snapshot.OpenPrice.HasValue && snapshot.HighPrice.HasValue && snapshot.LowPrice.HasValue)
        {
            decimal candleRange = snapshot.HighPrice.Value - snapshot.LowPrice.Value;
            if (candleRange > 0)
            {
                decimal upperWick = snapshot.HighPrice.Value - Math.Max(snapshot.OpenPrice.Value, snapshot.ClosePrice);
                if (upperWick > candleRange * 0.5m)
                {
                    score -= 10m;
                    reasons.Add("Intraday Upper Rejection Wick (Supply Overhead) (-10)");
                }
                else if (snapshot.ClosePrice > snapshot.OpenPrice.Value && (snapshot.HighPrice.Value - snapshot.ClosePrice) <= candleRange * 0.15m)
                {
                    score += 8m;
                    reasons.Add("Intraday Momentum Close at Candle High (+8)");
                }
            }
        }

        // Clamp final score strictly between 0 and 100
        score = Math.Clamp(score, 0m, 100m);
        string signal = CategorizeSignal(score);

        return new ScanScoreResult
        {
            Score = Math.Round(score, 1),
            Signal = signal,
            BreakoutStatus = breakoutStatus,
            VolumeStatus = volumeStatus,
            Explanation = string.Join("; ", reasons)
        };
    }

    private static string CategorizeSignal(decimal score)
    {
        return score switch
        {
            >= 80m => "Strong Bullish",
            >= 60m => "Bullish",
            >= 40m => "Neutral",
            >= 20m => "Bearish",
            _ => "Strong Bearish"
        };
    }
}
