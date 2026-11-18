namespace UpstoxAiScanner.Domain.Indicators;

/// <summary>
/// Quantitative analysis of Relative Strength Index (RSI).
/// </summary>
public record RsiAnalysis
{
    public decimal Value { get; init; }
    public bool IsOverbought => Value >= 70m;
    public bool IsOversold => Value <= 30m;
    public bool CrossedAbove30 { get; init; }
    public bool CrossedBelow70 { get; init; }
    public bool CrossedAbove50 { get; init; }
    public bool CrossedBelow50 { get; init; }
}

/// <summary>
/// Quantitative analysis of Moving Average Convergence Divergence (MACD).
/// </summary>
public record MacdAnalysis
{
    public decimal Macd { get; init; }
    public decimal Signal { get; init; }
    public decimal Histogram { get; init; }
    public bool IsBullishCrossover { get; init; }
    public bool IsBearishCrossover { get; init; }
    public bool IsAboveZero => Macd > 0m;
    public bool IsBelowZero => Macd < 0m;
    public string HistogramMomentum { get; init; } = "Neutral";
}

/// <summary>
/// Quantitative analysis of Exponential Moving Averages (EMA 20, 50, 200).
/// </summary>
public record EmaAnalysis
{
    public decimal? Ema20 { get; init; }
    public decimal? Ema50 { get; init; }
    public decimal? Ema200 { get; init; }

    public bool IsBullishAlignment => Ema20.HasValue && Ema50.HasValue && Ema200.HasValue &&
                                      Ema20.Value > Ema50.Value && Ema50.Value > Ema200.Value;

    public bool IsBearishAlignment => Ema20.HasValue && Ema50.HasValue && Ema200.HasValue &&
                                      Ema20.Value < Ema50.Value && Ema50.Value < Ema200.Value;

    public bool IsPriceAboveEma20 { get; init; }
    public bool IsPriceAboveEma50 { get; init; }
    public bool IsPriceAboveEma200 { get; init; }

    public bool CrossedAboveEma20 { get; init; }
    public bool CrossedBelowEma20 { get; init; }
    public bool GoldenCross { get; init; }
    public bool DeathCross { get; init; }
}

/// <summary>
/// Breakout and Breakdown analysis comparing current completed candle to historical levels.
/// Strictly excludes current candle from resistance/support calculations to prevent look-ahead bias.
/// </summary>
public record BreakoutAnalysis
{
    public bool Is20PeriodHighBreakout { get; init; }
    public bool Is50PeriodHighBreakout { get; init; }
    public bool Is20PeriodLowBreakdown { get; init; }
    public bool Is50PeriodLowBreakdown { get; init; }

    public decimal Resistance20 { get; init; }
    public decimal Resistance50 { get; init; }
    public decimal Support20 { get; init; }
    public decimal Support50 { get; init; }

    public bool IsVolumeConfirmed { get; init; }
}

/// <summary>
/// Trading volume metrics and surge detection.
/// </summary>
public record VolumeAnalysis
{
    public long CurrentVolume { get; init; }
    public decimal AverageVolume20 { get; init; }
    public decimal VolumeRatio { get; init; }
    public decimal RelativeVolume => VolumeRatio;
    public bool IsVolumeSurge { get; init; }
    public decimal SurgeThreshold { get; init; } = 1.5m;
}

/// <summary>
/// Quantitative analysis of Average True Range (ATR) for volatility and stop-loss modeling.
/// </summary>
public record AtrAnalysis
{
    public decimal Value { get; init; }
    public decimal AtrPercent => ClosePrice > 0 ? Math.Round((Value / ClosePrice) * 100m, 2) : 0m;
    public decimal ClosePrice { get; init; }
}

/// <summary>
/// Combined technical indicator snapshot for an instrument at a specific timestamp.
/// </summary>
public record TechnicalAnalysisSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }
    public decimal ClosePrice { get; init; }
    public decimal? OpenPrice { get; init; }
    public decimal? HighPrice { get; init; }
    public decimal? LowPrice { get; init; }
    public decimal? Atr { get; init; }
    public RsiAnalysis? Rsi { get; init; }
    public MacdAnalysis? Macd { get; init; }
    public EmaAnalysis? Ema { get; init; }
    public BreakoutAnalysis? Breakout { get; init; }
    public VolumeAnalysis? Volume { get; init; }
    public EmaAnalysis? HigherTimeframeEma { get; init; }
}
