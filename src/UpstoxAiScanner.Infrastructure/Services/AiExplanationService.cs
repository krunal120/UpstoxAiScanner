using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpstoxAiScanner.Application.Configuration;
using UpstoxAiScanner.Application.DTOs.Ai;
using UpstoxAiScanner.Application.DTOs.Scanner;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Infrastructure.Services;

/// <summary>
/// AI-powered technical explanation service generating structured institutional-grade market commentary.
/// Strictly grounded in actual quantitative indicator metrics; never hallucinates prices or trading signals.
/// Dual-mode: uses live OpenAI API when configured, and falls back to a deterministic financial narrative engine.
/// </summary>
public class AiExplanationService : IAiExplanationService
{
    private readonly IScannerService _scannerService;
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<AiExplanationService> _logger;

    public AiExplanationService(
        IScannerService scannerService,
        HttpClient httpClient,
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<AiExplanationService> logger)
    {
        _scannerService = scannerService;
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<AiExplanationDto> ExplainScanResultAsync(
        ScanResultDto scanResult,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;

        if (options.EnableAiExplanations && options.IsConfigured)
        {
            try
            {
                var liveExplanation = await GenerateOpenAiExplanationAsync(scanResult, options, cancellationToken);
                if (liveExplanation != null)
                {
                    return liveExplanation;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI explanation call failed for {Symbol}, falling back to rule-based engine: {Message}", scanResult.Symbol, ex.Message);
            }
        }

        // Deterministic Financial Narrative Engine fallback
        return GenerateDeterministicExplanation(scanResult);
    }

    public async Task<AiExplanationDto?> ExplainStockAsync(
        string symbol,
        string scanType = "Swing",
        CancellationToken cancellationToken = default)
    {
        var scanResult = await _scannerService.ScanSingleInstrumentAsync(symbol, scanType, cancellationToken);
        if (scanResult == null)
            return null;

        return await ExplainScanResultAsync(scanResult, cancellationToken);
    }

    private async Task<AiExplanationDto?> GenerateOpenAiExplanationAsync(
        ScanResultDto scanResult,
        AiOptions options,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"You are a Senior Quantitative Analyst and Technical Strategist for the Indian Stock Market (NSE/BSE).
Your task is to provide an institutional-grade technical commentary explaining pre-computed indicator data for a stock.
CRITICAL RULES:
1. NEVER invent, alter, or fabricate prices, support/resistance levels, indicators, or scores.
2. Base your analysis 100% on the factual quantitative metrics provided.
3. Output strictly valid JSON matching this schema:
{
  ""executiveSummary"": ""string (2-3 crisp sentences summarizing the setup)"",
  ""keyDrivers"": [""string""],
  ""supportResistanceLevels"": ""string (analysis of dynamic EMA levels and breakout barriers)"",
  ""riskFactors"": ""string (invalidation levels and momentum exhaustion risks)"",
  ""fullCommentary"": ""string (comprehensive multi-paragraph market commentary)""
}";

        var userPayload = new
        {
            symbol = scanResult.Symbol,
            companyName = scanResult.CompanyName,
            scanType = scanResult.ScanType,
            currentPrice = scanResult.CurrentPrice,
            technicalScore = scanResult.TechnicalScore,
            signal = scanResult.Signal,
            breakoutStatus = scanResult.BreakoutStatus,
            volumeStatus = scanResult.VolumeStatus,
            rsi = scanResult.Rsi,
            macd = scanResult.Macd,
            ema20 = scanResult.Ema20,
            ema50 = scanResult.Ema50,
            ema200 = scanResult.Ema200,
            volumeRatio = scanResult.VolumeRatio,
            ruleDetails = scanResult.Explanation
        };

        var requestBody = new
        {
            model = options.Model,
            temperature = (double)options.Temperature,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = JsonSerializer.Serialize(userPayload) }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("OpenAI API returned non-success code {Code}: {Error}", response.StatusCode, err);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        var contentText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(contentText))
            return null;

        var parsed = JsonSerializer.Deserialize<JsonElement>(contentText);

        var driversList = new List<string>();
        if (parsed.TryGetProperty("keyDrivers", out var driversElem) && driversElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in driversElem.EnumerateArray())
            {
                driversList.Add(item.GetString() ?? string.Empty);
            }
        }

        return new AiExplanationDto
        {
            Symbol = scanResult.Symbol,
            CompanyName = scanResult.CompanyName,
            ScanType = scanResult.ScanType,
            CurrentPrice = scanResult.CurrentPrice,
            TechnicalScore = scanResult.TechnicalScore,
            Signal = scanResult.Signal,
            ExecutiveSummary = parsed.TryGetProperty("executiveSummary", out var exSum) ? (exSum.GetString() ?? string.Empty) : string.Empty,
            KeyDrivers = driversList,
            SupportResistanceLevels = parsed.TryGetProperty("supportResistanceLevels", out var sr) ? (sr.GetString() ?? string.Empty) : string.Empty,
            RiskFactors = parsed.TryGetProperty("riskFactors", out var rf) ? (rf.GetString() ?? string.Empty) : string.Empty,
            FullCommentary = parsed.TryGetProperty("fullCommentary", out var fc) ? (fc.GetString() ?? string.Empty) : string.Empty,
            Provider = "OpenAI",
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    private static AiExplanationDto GenerateDeterministicExplanation(ScanResultDto scanResult)
    {
        var rawRules = (scanResult.Explanation ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var keyDrivers = rawRules.Count > 0
            ? rawRules
            : new List<string> { "Baseline technical posture without strong directional confluence." };

        string execSummary;
        string supportResistance;
        string riskFactors;
        var commentary = new StringBuilder();

        if (scanResult.TechnicalScore >= 80m)
        {
            execSummary = $"{scanResult.Symbol} ({scanResult.CompanyName}) displays an exceptionally strong bullish posture with a technical score of {scanResult.TechnicalScore:F1}/100. Strong multi-timeframe trend alignment and momentum confluence indicate sustained institutional accumulation.";
            supportResistance = $"Immediate dynamic support is anchored by the 20-period EMA ({scanResult.Ema20:F2}) and 50-period EMA ({scanResult.Ema50:F2}). A decisive hold above {scanResult.Ema200:F2} (200 EMA) reinforces structural uptrend integrity.";
            riskFactors = scanResult.Rsi >= 70m
                ? $"RSI is elevated at {scanResult.Rsi:F1}, warranting caution for short-term mean-reversion pauses or consolidation before continuation."
                : "A breach below the 20 EMA would serve as an early invalidation signal of short-term momentum.";
        }
        else if (scanResult.TechnicalScore >= 60m)
        {
            execSummary = $"{scanResult.Symbol} exhibits constructive bullish momentum with a technical score of {scanResult.TechnicalScore:F1}/100. Price action is trading favorably above core moving averages with positive momentum.";
            supportResistance = $"Support resides at the rising 20 EMA ({scanResult.Ema20:F2}), while previous swing resistance is currently being tested.";
            riskFactors = "Watch for volume consistency; a lack of sustained relative volume could result in a sideways consolidation range.";
        }
        else if (scanResult.TechnicalScore >= 40m)
        {
            execSummary = $"{scanResult.Symbol} is currently traversing a neutral, balanced technical phase with a score of {scanResult.TechnicalScore:F1}/100. Bullish indicators and overhead supply factors are evenly matched.";
            supportResistance = $"The stock is oscillating between the 50 EMA ({scanResult.Ema50:F2}) as support and the 20 EMA ({scanResult.Ema20:F2}) as immediate resistance.";
            riskFactors = "High risk of choppy price action within the current congestion band until a clear directional breakout emerges.";
        }
        else if (scanResult.TechnicalScore >= 20m)
        {
            execSummary = $"{scanResult.Symbol} displays deteriorating technical structure with a bearish score of {scanResult.TechnicalScore:F1}/100. Selling pressure and moving average overhead are exerting downward momentum.";
            supportResistance = $"Prior support has now converted into overhead resistance at {scanResult.Ema20:F2}. Downside trajectory targets lower support zones.";
            riskFactors = "Potential for accelerating downside momentum if key swing lows fail to hold.";
        }
        else
        {
            execSummary = $"{scanResult.Symbol} is under severe distribution with a strong bearish score of {scanResult.TechnicalScore:F1}/100. Significant trend breakdowns across multiple indicators reflect dominant selling pressure.";
            supportResistance = $"All major moving averages (EMA20: {scanResult.Ema20:F2}, EMA50: {scanResult.Ema50:F2}, EMA200: {scanResult.Ema200:F2}) are downward sloping and act as firm resistance ceilings.";
            riskFactors = "High risk of continued capitulation until an RSI bullish divergence or volume-backed bottoming pattern appears.";
        }

        commentary.AppendLine($"### Technical Overview for {scanResult.Symbol} ({scanResult.ScanType} Horizon)");
        commentary.AppendLine();
        commentary.AppendLine($"At current price of **₹{scanResult.CurrentPrice:F2}**, {scanResult.Symbol} carries a technical score of **{scanResult.TechnicalScore:F1}/100**, placing it in the **{scanResult.Signal}** classification.");
        commentary.AppendLine();
        commentary.AppendLine("#### Core Momentum & Trend Dynamics");
        if (scanResult.Rsi.HasValue) commentary.AppendLine($"- **RSI (14)**: {scanResult.Rsi.Value:F1} (Wilder's smoothed)");
        if (scanResult.Macd.HasValue) commentary.AppendLine($"- **MACD**: {scanResult.Macd.Value:F2}");
        if (scanResult.Ema20.HasValue && scanResult.Ema50.HasValue) commentary.AppendLine($"- **Moving Averages**: EMA20 = ₹{scanResult.Ema20.Value:F2}, EMA50 = ₹{scanResult.Ema50.Value:F2}, EMA200 = ₹{scanResult.Ema200:F2}");
        if (!string.IsNullOrWhiteSpace(scanResult.BreakoutStatus) && scanResult.BreakoutStatus != "None") commentary.AppendLine($"- **Breakout Status**: {scanResult.BreakoutStatus}");
        if (!string.IsNullOrWhiteSpace(scanResult.VolumeStatus) && scanResult.VolumeStatus != "Normal") commentary.AppendLine($"- **Volume Activity**: {scanResult.VolumeStatus}");
        commentary.AppendLine();
        commentary.AppendLine("#### Strategic Conclusion");
        commentary.AppendLine(execSummary);

        return new AiExplanationDto
        {
            Symbol = scanResult.Symbol,
            CompanyName = scanResult.CompanyName,
            ScanType = scanResult.ScanType,
            CurrentPrice = scanResult.CurrentPrice,
            TechnicalScore = scanResult.TechnicalScore,
            Signal = scanResult.Signal,
            ExecutiveSummary = execSummary,
            KeyDrivers = keyDrivers,
            SupportResistanceLevels = supportResistance,
            RiskFactors = riskFactors,
            FullCommentary = commentary.ToString(),
            Provider = "RuleBasedFinancialEngine",
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }
}
