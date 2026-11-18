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
/// Dual-mode: supports Google Gemini Pro / Flash and OpenAI, falling back to a deterministic financial narrative engine.
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
                AiExplanationDto? liveExplanation;

                bool isGemini = options.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) ||
                                options.Model.Contains("gemini", StringComparison.OrdinalIgnoreCase) ||
                                options.BaseUrl.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase);

                if (isGemini)
                {
                    liveExplanation = await GenerateGeminiExplanationAsync(scanResult, options, cancellationToken);
                }
                else
                {
                    liveExplanation = await GenerateOpenAiExplanationAsync(scanResult, options, cancellationToken);
                }

                if (liveExplanation != null)
                {
                    return liveExplanation;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live AI explanation call failed for {Symbol}, falling back to rule-based engine: {Message}", scanResult.Symbol, ex.Message);
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

    private async Task<AiExplanationDto?> GenerateGeminiExplanationAsync(
        ScanResultDto scanResult,
        AiOptions options,
        CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(options.Model) ? "gemini-flash-lite-latest" : options.Model;
        var baseUrl = options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/models/{model}:generateContent?key={options.ApiKey}";

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
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = $"Analyze the following stock quantitative metrics:\n{JsonSerializer.Serialize(userPayload)}" }
                    }
                }
            },
            systemInstruction = new
            {
                parts = new object[]
                {
                    new { text = systemPrompt }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = (double)options.Temperature
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Gemini API returned non-success code {Code}: {Error}", response.StatusCode, err);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        var contentText = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(contentText))
            return null;

        var parsed = JsonSerializer.Deserialize<JsonElement>(contentText);

        var driversList = new List<string>();
        if (parsed.TryGetProperty("keyDrivers", out var driversElem) && driversElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in driversElem.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s)) driversList.Add(s);
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
            Provider = $"Google Gemini ({model})",
            GeneratedAt = DateTimeOffset.UtcNow
        };
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
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s)) driversList.Add(s);
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
            execSummary = $"{scanResult.Symbol} exhibits constructive bullish characteristics scored at {scanResult.TechnicalScore:F1}/100. Indicators lean favorable for trend continuation or breakout setups.";
            supportResistance = $"Key support zone resides between the 20 EMA ({scanResult.Ema20:F2}) and 50 EMA ({scanResult.Ema50:F2}). Overhead resistance aligns with recent swing highs.";
            riskFactors = "Failure to sustain volume confirmation could lead to range-bound chop.";
        }
        else if (scanResult.TechnicalScore >= 40m)
        {
            execSummary = $"{scanResult.Symbol} is currently consolidating in a neutral technical regime with a score of {scanResult.TechnicalScore:F1}/100. Market participants are balancing supply and demand.";
            supportResistance = $"Price is oscillating around intermediate moving averages (50 EMA at {scanResult.Ema50:F2}). Major structural support sits near the 200 EMA ({scanResult.Ema200:F2}).";
            riskFactors = "Directional conviction is low; avoid pre-empting breakouts without clear volume expansion.";
        }
        else if (scanResult.TechnicalScore >= 20m)
        {
            execSummary = $"{scanResult.Symbol} reflects a bearish structural tilt scored at {scanResult.TechnicalScore:F1}/100. Distribution pressure and weakening momentum dominate the timeframe.";
            supportResistance = $"Dynamic moving averages (20 EMA {scanResult.Ema20:F2}, 50 EMA {scanResult.Ema50:F2}) now act as overhead resistance barriers on pullback attempts.";
            riskFactors = "Downside momentum continuation remains the dominant probability unless 50 EMA is reclaimed with high volume.";
        }
        else
        {
            execSummary = $"{scanResult.Symbol} indicates severe bearish technical deterioration scored at {scanResult.TechnicalScore:F1}/100. Indicators reflect dominant selling pressure and downward momentum acceleration.";
            supportResistance = $"All major exponential moving averages (20 EMA {scanResult.Ema20:F2}, 50 EMA {scanResult.Ema50:F2}, 200 EMA {scanResult.Ema200:F2}) are negatively stacked overhead.";
            riskFactors = "High probability of further downside discovery. Any technical bounces should be treated as counter-trend rallies.";
        }

        commentary.AppendLine($"### Institutional Overview: {scanResult.Symbol}");
        commentary.AppendLine(execSummary);
        commentary.AppendLine();
        commentary.AppendLine("### Quantitative Indicators Breakdown");
        commentary.AppendLine($"- **Technical Score**: {scanResult.TechnicalScore:F1} / 100 ({scanResult.Signal})");
        commentary.AppendLine($"- **RSI (14-period Wilder's)**: {scanResult.Rsi:F2}");
        commentary.AppendLine($"- **MACD (12/26/9)**: Value = {scanResult.Macd:F2}");
        commentary.AppendLine($"- **Moving Averages**: 20 EMA = ₹{scanResult.Ema20:F2} | 50 EMA = ₹{scanResult.Ema50:F2} | 200 EMA = ₹{scanResult.Ema200:F2}");
        commentary.AppendLine($"- **Volume Posture**: {scanResult.VolumeStatus} (Volume Ratio: {scanResult.VolumeRatio:F2}x 20-day avg)");
        commentary.AppendLine($"- **Breakout Status**: {scanResult.BreakoutStatus}");
        commentary.AppendLine();
        commentary.AppendLine("### Strategy Implications");
        commentary.AppendLine(supportResistance);
        commentary.AppendLine();
        commentary.AppendLine("### Risk & Invalidation");
        commentary.AppendLine(riskFactors);

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
