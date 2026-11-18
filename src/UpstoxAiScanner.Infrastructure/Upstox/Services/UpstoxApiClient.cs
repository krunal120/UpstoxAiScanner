using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.Configuration;
using UpstoxAiScanner.Application.DTOs.MarketData;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Infrastructure.Upstox.Models;

namespace UpstoxAiScanner.Infrastructure.Upstox.Services;

/// <summary>
/// Production-grade implementation of Upstox Market Data API client.
/// Adheres strictly to official Upstox V3 API specifications with automatic throttling, resilience, and mock fallback.
/// Supports both configuration-based tokens (appsettings/User Secrets/Env) and per-request tokens (Swagger Authorize header).
/// </summary>
public class UpstoxApiClient : IUpstoxMarketDataService
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<UpstoxOptions> _optionsMonitor;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly UpstoxRateLimiter _rateLimiter;
    private readonly MockUpstoxDataProvider _mockProvider;
    private readonly ILogger<UpstoxApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UpstoxApiClient(
        HttpClient httpClient,
        IOptionsMonitor<UpstoxOptions> optionsMonitor,
        UpstoxRateLimiter rateLimiter,
        MockUpstoxDataProvider mockProvider,
        ILogger<UpstoxApiClient> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _rateLimiter = rateLimiter;
        _mockProvider = mockProvider;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Resolves the effective access token by checking:
    /// 1. Incoming HTTP Authorization header (e.g., from Swagger's Authorize button)
    /// 2. appsettings / User Secrets / Environment Variable
    /// </summary>
    public string? GetEffectiveToken()
    {
        // 1. Check if caller supplied Authorization header (e.g. from Swagger Authorize dialog)
        var context = _httpContextAccessor?.HttpContext;
        if (context != null && context.Request.Headers.TryGetValue("Authorization", out var authValues))
        {
            var headerVal = authValues.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(headerVal))
            {
                if (headerVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = headerVal.Substring("Bearer ".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        return token;
                    }
                }
                else
                {
                    // User pasted the raw JWT token in Swagger without typing 'Bearer '
                    return headerVal;
                }
            }
        }

        // 2. Fall back to configured token in appsettings / User Secrets / Environment
        var configToken = _optionsMonitor.CurrentValue.AccessToken;
        if (!string.IsNullOrWhiteSpace(configToken))
        {
            return configToken.Trim();
        }

        return null;
    }

    public bool IsLiveMode()
    {
        if (_optionsMonitor.CurrentValue.ForceMockMode)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(GetEffectiveToken());
    }

    public async Task<ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>> GetMarketQuotesAsync(
        IEnumerable<string> instrumentKeys,
        CancellationToken cancellationToken = default)
    {
        var keysList = instrumentKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keysList.Count == 0)
        {
            return ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>.Fail("No instrument keys provided.", "EMPTY_INSTRUMENTS");
        }

        if (!IsLiveMode())
        {
            _logger.LogInformation("Using mock market quotes data for {Count} instruments.", keysList.Count);
            var mockData = _mockProvider.GetQuotes(keysList);
            return ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>.Ok(mockData, isMockData: true);
        }

        try
        {
            await _rateLimiter.WaitForSlotAsync(cancellationToken);
            var commaSeparatedKeys = string.Join(",", keysList);
            var requestUri = $"v3/market-quote/quotes?instrument_key={Uri.EscapeDataString(commaSeparatedKeys)}";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await HandleErrorResponseAsync<IReadOnlyDictionary<string, MarketQuoteDto>>(response, () => _mockProvider.GetQuotes(keysList));
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var upstoxResponse = await JsonSerializer.DeserializeAsync<UpstoxApiResponse<Dictionary<string, UpstoxRawMarketQuote>>>(
                jsonStream, JsonOptions, cancellationToken);

            if (upstoxResponse?.Data == null)
            {
                return ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>.Fail("Upstox returned empty data.", "EMPTY_RESPONSE");
            }

            var result = new Dictionary<string, MarketQuoteDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, rawQuote) in upstoxResponse.Data)
            {
                var dto = MapToMarketQuoteDto(key, rawQuote);
                result[key] = dto;
                if (!string.IsNullOrWhiteSpace(rawQuote.InstrumentToken) && !result.ContainsKey(rawQuote.InstrumentToken))
                {
                    result[rawQuote.InstrumentToken] = dto;
                }
            }

            return ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>.Ok(result, isMockData: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch market quotes from Upstox API.");
            if (_optionsMonitor.CurrentValue.UseMockFallbackWhenUnconfigured)
            {
                _logger.LogWarning("Falling back to mock market quotes due to request exception: {Message}", ex.Message);
                return ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>.Ok(_mockProvider.GetQuotes(keysList), isMockData: true);
            }
            return ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>.Fail(ex.Message, "HTTP_EXCEPTION");
        }
    }

    public async Task<ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>> GetLtpQuotesAsync(
        IEnumerable<string> instrumentKeys,
        CancellationToken cancellationToken = default)
    {
        var keysList = instrumentKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keysList.Count == 0)
        {
            return ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>.Fail("No instrument keys provided.", "EMPTY_INSTRUMENTS");
        }

        if (!IsLiveMode())
        {
            _logger.LogInformation("Using mock LTP data for {Count} instruments.", keysList.Count);
            var mockData = _mockProvider.GetLtpQuotes(keysList);
            return ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>.Ok(mockData, isMockData: true);
        }

        try
        {
            await _rateLimiter.WaitForSlotAsync(cancellationToken);
            var commaSeparatedKeys = string.Join(",", keysList);
            var requestUri = $"v3/market-quote/ltp?instrument_key={Uri.EscapeDataString(commaSeparatedKeys)}";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await HandleErrorResponseAsync<IReadOnlyDictionary<string, LtpQuoteDto>>(response, () => _mockProvider.GetLtpQuotes(keysList));
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var upstoxResponse = await JsonSerializer.DeserializeAsync<UpstoxApiResponse<Dictionary<string, UpstoxRawLtpQuote>>>(
                jsonStream, JsonOptions, cancellationToken);

            if (upstoxResponse?.Data == null)
            {
                return ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>.Fail("Upstox returned empty LTP data.", "EMPTY_RESPONSE");
            }

            var result = new Dictionary<string, LtpQuoteDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, rawLtp) in upstoxResponse.Data)
            {
                var lastPrice = rawLtp.LastPrice ?? 0m;
                var cp = rawLtp.ClosePrice ?? 0m;
                var change = lastPrice - cp;
                var changePct = cp != 0 ? Math.Round((change / cp) * 100, 2) : 0m;

                var dto = new LtpQuoteDto
                {
                    InstrumentKey = key,
                    LastPrice = lastPrice,
                    LastTradedQuantity = (long)(rawLtp.LastTradedQuantity ?? 0m),
                    Volume = (long)(rawLtp.Volume ?? 0m),
                    ClosePrice = cp,
                    Change = change,
                    ChangePercentage = changePct
                };

                result[key] = dto;
                if (!string.IsNullOrWhiteSpace(rawLtp.InstrumentToken) && !result.ContainsKey(rawLtp.InstrumentToken))
                {
                    result[rawLtp.InstrumentToken] = dto;
                }
            }

            return ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>.Ok(result, isMockData: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch LTP quotes from Upstox API.");
            if (_optionsMonitor.CurrentValue.UseMockFallbackWhenUnconfigured)
            {
                _logger.LogWarning("Falling back to mock LTP data due to exception: {Message}", ex.Message);
                return ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>.Ok(_mockProvider.GetLtpQuotes(keysList), isMockData: true);
            }
            return ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>.Fail(ex.Message, "HTTP_EXCEPTION");
        }
    }

    public async Task<ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>> GetOhlcQuotesAsync(
        IEnumerable<string> instrumentKeys,
        string interval = "1d",
        CancellationToken cancellationToken = default)
    {
        var keysList = instrumentKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keysList.Count == 0)
        {
            return ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>.Fail("No instrument keys provided.", "EMPTY_INSTRUMENTS");
        }

        if (!IsLiveMode())
        {
            _logger.LogInformation("Using mock OHLC data for {Count} instruments.", keysList.Count);
            var mockData = _mockProvider.GetOhlcQuotes(keysList, interval);
            return ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>.Ok(mockData, isMockData: true);
        }

        try
        {
            await _rateLimiter.WaitForSlotAsync(cancellationToken);
            var commaSeparatedKeys = string.Join(",", keysList);
            var requestUri = $"v3/market-quote/ohlc?instrument_key={Uri.EscapeDataString(commaSeparatedKeys)}&interval={Uri.EscapeDataString(interval)}";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await HandleErrorResponseAsync<IReadOnlyDictionary<string, OhlcQuoteDto>>(response, () => _mockProvider.GetOhlcQuotes(keysList, interval));
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var upstoxResponse = await JsonSerializer.DeserializeAsync<UpstoxApiResponse<Dictionary<string, UpstoxRawOhlcQuote>>>(
                jsonStream, JsonOptions, cancellationToken);

            if (upstoxResponse?.Data == null)
            {
                return ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>.Fail("Upstox returned empty OHLC data.", "EMPTY_RESPONSE");
            }

            var result = new Dictionary<string, OhlcQuoteDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, rawOhlc) in upstoxResponse.Data)
            {
                var ohlcData = rawOhlc.LiveOhlc ?? rawOhlc.Ohlc;
                var dto = new OhlcQuoteDto
                {
                    InstrumentKey = key,
                    LastPrice = rawOhlc.LastPrice ?? 0m,
                    Open = ohlcData?.Open ?? 0m,
                    High = ohlcData?.High ?? 0m,
                    Low = ohlcData?.Low ?? 0m,
                    Close = ohlcData?.Close ?? 0m,
                    Volume = (long)(ohlcData?.Volume ?? 0m),
                    Timestamp = ParseTimestamp(ohlcData?.Timestamp),
                    PrevOpen = rawOhlc.PrevOhlc?.Open,
                    PrevHigh = rawOhlc.PrevOhlc?.High,
                    PrevLow = rawOhlc.PrevOhlc?.Low,
                    PrevClose = rawOhlc.PrevOhlc?.Close,
                    PrevVolume = rawOhlc.PrevOhlc?.Volume.HasValue == true ? (long)rawOhlc.PrevOhlc.Volume.Value : null
                };

                result[key] = dto;
                if (!string.IsNullOrWhiteSpace(rawOhlc.InstrumentToken) && !result.ContainsKey(rawOhlc.InstrumentToken))
                {
                    result[rawOhlc.InstrumentToken] = dto;
                }
            }

            return ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>.Ok(result, isMockData: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch OHLC quotes from Upstox API.");
            if (_optionsMonitor.CurrentValue.UseMockFallbackWhenUnconfigured)
            {
                _logger.LogWarning("Falling back to mock OHLC quotes due to exception: {Message}", ex.Message);
                return ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>.Ok(_mockProvider.GetOhlcQuotes(keysList, interval), isMockData: true);
            }
            return ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>.Fail(ex.Message, "HTTP_EXCEPTION");
        }
    }

    public async Task<ApiResponse<IReadOnlyList<CandleDto>>> GetHistoricalCandlesAsync(
        string instrumentKey,
        string unit,
        int interval,
        DateOnly toDate,
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrumentKey))
        {
            return ApiResponse<IReadOnlyList<CandleDto>>.Fail("Instrument key must not be empty.", "INVALID_INSTRUMENT");
        }

        if (toDate < fromDate)
        {
            return ApiResponse<IReadOnlyList<CandleDto>>.Fail("ToDate must be greater than or equal to FromDate.", "INVALID_DATE_RANGE");
        }

        if (!IsLiveMode())
        {
            _logger.LogInformation("Using mock historical candles for {InstrumentKey}", instrumentKey);
            var mockCandles = _mockProvider.GetHistoricalCandles(instrumentKey, unit, interval, toDate, fromDate);
            return ApiResponse<IReadOnlyList<CandleDto>>.Ok(mockCandles, isMockData: true);
        }

        try
        {
            await _rateLimiter.WaitForSlotAsync(cancellationToken);
            var encodedKey = Uri.EscapeDataString(instrumentKey);
            var toDateStr = toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var fromDateStr = fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var normalizedUnit = unit.ToLowerInvariant();
            var requestUri = $"v3/historical-candle/{encodedKey}/{normalizedUnit}/{interval}/{toDateStr}/{fromDateStr}";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await HandleErrorResponseAsync<IReadOnlyList<CandleDto>>(response, () => _mockProvider.GetHistoricalCandles(instrumentKey, unit, interval, toDate, fromDate));
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var upstoxResponse = await JsonSerializer.DeserializeAsync<UpstoxApiResponse<UpstoxRawCandlesData>>(
                jsonStream, JsonOptions, cancellationToken);

            var candles = ParseCandleList(upstoxResponse?.Data?.Candles);
            return ApiResponse<IReadOnlyList<CandleDto>>.Ok(candles, isMockData: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical candles for {InstrumentKey}", instrumentKey);
            if (_optionsMonitor.CurrentValue.UseMockFallbackWhenUnconfigured)
            {
                _logger.LogWarning("Falling back to mock historical candles due to exception: {Message}", ex.Message);
                return ApiResponse<IReadOnlyList<CandleDto>>.Ok(_mockProvider.GetHistoricalCandles(instrumentKey, unit, interval, toDate, fromDate), isMockData: true);
            }
            return ApiResponse<IReadOnlyList<CandleDto>>.Fail(ex.Message, "HTTP_EXCEPTION");
        }
    }

    public async Task<ApiResponse<IReadOnlyList<CandleDto>>> GetIntradayCandlesAsync(
        string instrumentKey,
        string unit,
        int interval,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrumentKey))
        {
            return ApiResponse<IReadOnlyList<CandleDto>>.Fail("Instrument key must not be empty.", "INVALID_INSTRUMENT");
        }

        if (!IsLiveMode())
        {
            _logger.LogInformation("Using mock intraday candles for {InstrumentKey}", instrumentKey);
            var mockCandles = _mockProvider.GetIntradayCandles(instrumentKey, unit, interval);
            return ApiResponse<IReadOnlyList<CandleDto>>.Ok(mockCandles, isMockData: true);
        }

        try
        {
            await _rateLimiter.WaitForSlotAsync(cancellationToken);
            var encodedKey = Uri.EscapeDataString(instrumentKey);
            var normalizedUnit = unit.ToLowerInvariant();
            var requestUri = $"v3/historical-candle/intraday/{encodedKey}/{normalizedUnit}/{interval}";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await HandleErrorResponseAsync<IReadOnlyList<CandleDto>>(response, () => _mockProvider.GetIntradayCandles(instrumentKey, unit, interval));
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var upstoxResponse = await JsonSerializer.DeserializeAsync<UpstoxApiResponse<UpstoxRawCandlesData>>(
                jsonStream, JsonOptions, cancellationToken);

            var candles = ParseCandleList(upstoxResponse?.Data?.Candles);
            return ApiResponse<IReadOnlyList<CandleDto>>.Ok(candles, isMockData: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch intraday candles for {InstrumentKey}", instrumentKey);
            if (_optionsMonitor.CurrentValue.UseMockFallbackWhenUnconfigured)
            {
                _logger.LogWarning("Falling back to mock intraday candles due to exception: {Message}", ex.Message);
                return ApiResponse<IReadOnlyList<CandleDto>>.Ok(_mockProvider.GetIntradayCandles(instrumentKey, unit, interval), isMockData: true);
            }
            return ApiResponse<IReadOnlyList<CandleDto>>.Fail(ex.Message, "HTTP_EXCEPTION");
        }
    }

    public Task<UpstoxStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var effectiveToken = GetEffectiveToken();
        var hasToken = !string.IsNullOrWhiteSpace(effectiveToken);
        var isLive = IsLiveMode();

        var tokenSource = "None";
        var context = _httpContextAccessor?.HttpContext;
        if (context != null && context.Request.Headers.ContainsKey("Authorization"))
        {
            tokenSource = "Swagger / HTTP Authorization Header";
        }
        else if (!string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.AccessToken))
        {
            tokenSource = "Configuration (appsettings / User Secrets / Environment)";
        }

        var message = isLive
            ? $"Live Upstox API mode active. Token loaded via: {tokenSource}."
            : hasToken && _optionsMonitor.CurrentValue.ForceMockMode
                ? "Mock mode forced via configuration."
                : "No Upstox Access Token configured. Running in Mock Data mode. Provide a token in Swagger Authorize or appsettings.json.";

        var status = new UpstoxStatusDto
        {
            IsLiveMode = isLive,
            HasTokenConfigured = hasToken,
            BaseUrl = _optionsMonitor.CurrentValue.BaseUrl,
            ApiVersion = _optionsMonitor.CurrentValue.ApiVersion,
            StatusMessage = message,
            ServerTimeUtc = DateTimeOffset.UtcNow
        };

        return Task.FromResult(status);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var token = GetEffectiveToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<ApiResponse<T>> HandleErrorResponseAsync<T>(HttpResponseMessage response, Func<T> fallbackMockFactory)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        _logger.LogWarning("Upstox API returned error status {StatusCode}: {Content}", response.StatusCode, errorContent);

        // If the user supplied a token and Upstox rejected it, fail explicitly so the caller knows the token is invalid/expired!
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            return ApiResponse<T>.Fail(
                $"Upstox API Authentication Failed: HTTP {(int)response.StatusCode} {response.StatusCode}. Your Upstox Access Token may be expired or invalid. Details: {errorContent}",
                response.StatusCode.ToString());
        }

        if (_optionsMonitor.CurrentValue.UseMockFallbackWhenUnconfigured)
        {
            _logger.LogInformation("Using mock fallback data due to Upstox API HTTP {StatusCode}", response.StatusCode);
            return ApiResponse<T>.Ok(fallbackMockFactory(), isMockData: true, message: $"Upstox API returned HTTP {response.StatusCode}. Mock fallback applied.");
        }

        return ApiResponse<T>.Fail($"Upstox API returned {response.StatusCode}: {errorContent}", response.StatusCode.ToString());
    }

    private static IReadOnlyList<CandleDto> ParseCandleList(List<List<JsonElement>>? rawCandles)
    {
        if (rawCandles == null || rawCandles.Count == 0)
        {
            return Array.Empty<CandleDto>();
        }

        var result = new List<CandleDto>(rawCandles.Count);
        foreach (var candleArray in rawCandles)
        {
            if (candleArray.Count < 6) continue;

            // Upstox candle array format: [timestamp, open, high, low, close, volume, oi (optional)]
            var timestampStr = candleArray[0].GetString();
            if (!DateTimeOffset.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            {
                continue;
            }

            var open = candleArray[1].GetDecimal();
            var high = candleArray[2].GetDecimal();
            var low = candleArray[3].GetDecimal();
            var close = candleArray[4].GetDecimal();
            var volume = candleArray[5].GetInt64();
            var oi = candleArray.Count > 6 && candleArray[6].ValueKind != JsonValueKind.Null ? candleArray[6].GetDecimal() : 0m;

            result.Add(new CandleDto
            {
                Timestamp = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                OpenInterest = oi
            });
        }

        return result;
    }

    private static MarketQuoteDto MapToMarketQuoteDto(string key, UpstoxRawMarketQuote raw)
    {
        var open = raw.Ohlc?.Open ?? 0m;
        var high = raw.Ohlc?.High ?? 0m;
        var low = raw.Ohlc?.Low ?? 0m;
        var close = raw.Ohlc?.Close ?? 0m;
        var lastPrice = raw.LastPrice ?? close;
        var prevClose = raw.PrevClosePrice ?? 0m;
        var netChange = raw.NetChange ?? (lastPrice - prevClose);
        var pctChange = prevClose != 0 ? Math.Round((netChange / prevClose) * 100, 2) : 0m;

        var depthDto = new MarketDepthDto
        {
            Buy = raw.Depth?.Buy?.Select(b => new MarketDepthItemDto
            {
                Quantity = (int)b.Quantity,
                Price = b.Price,
                Orders = b.Orders
            }).ToList() ?? new List<MarketDepthItemDto>(),
            Sell = raw.Depth?.Sell?.Select(s => new MarketDepthItemDto
            {
                Quantity = (int)s.Quantity,
                Price = s.Price,
                Orders = s.Orders
            }).ToList() ?? new List<MarketDepthItemDto>()
        };

        var rawVol = raw.Volume ?? raw.Ohlc?.Volume ?? 0m;

        return new MarketQuoteDto
        {
            InstrumentKey = key,
            Symbol = raw.Symbol ?? key,
            LastPrice = lastPrice,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = (long)rawVol,
            AveragePrice = raw.AveragePrice ?? 0m,
            NetChange = netChange,
            PercentageChange = pctChange,
            TotalBuyQuantity = (long)(raw.TotalBuyQuantity ?? 0m),
            TotalSellQuantity = (long)(raw.TotalSellQuantity ?? 0m),
            LowerCircuitLimit = raw.LowerCircuitLimit ?? 0m,
            UpperCircuitLimit = raw.UpperCircuitLimit ?? 0m,
            PrevClosePrice = prevClose,
            YearHigh = raw.YearHigh ?? 0m,
            YearLow = raw.YearLow ?? 0m,
            OpenInterest = raw.OpenInterest ?? 0m,
            LastTradeTime = ParseTimestamp(raw.LastTradeTime),
            Timestamp = ParseTimestamp(raw.Timestamp) ?? DateTimeOffset.UtcNow,
            Depth = depthDto
        };
    }

    private static DateTimeOffset? ParseTimestamp(JsonElement? element)
    {
        if (element == null || !element.HasValue) return null;

        var val = element.Value;
        if (val.ValueKind == JsonValueKind.Number)
        {
            if (val.TryGetInt64(out var epochMs))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
            }
        }
        else if (val.ValueKind == JsonValueKind.String)
        {
            var str = val.GetString();
            if (string.IsNullOrWhiteSpace(str)) return null;

            if (long.TryParse(str, out var epochMs))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
            }

            if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            {
                return dto;
            }
        }

        return null;
    }
}
