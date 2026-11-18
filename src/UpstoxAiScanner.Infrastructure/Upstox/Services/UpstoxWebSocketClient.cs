using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpstoxAiScanner.Application.Configuration;
using UpstoxAiScanner.Application.DTOs.MarketData;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Infrastructure.Upstox.Services;

/// <summary>
/// Upstox Market Data WebSocket V3 client providing real-time price & volume streaming.
/// Features dual-mode operation: connects to live Upstox WebSocket when authenticated,
/// and produces realistic simulated market tick feeds when unauthenticated or in offline mode.
/// </summary>
public class UpstoxWebSocketClient : IUpstoxWebSocketClient
{
    private readonly IOptionsMonitor<UpstoxOptions> _upstoxOptions;
    private readonly IOptionsMonitor<ScannerBackgroundOptions> _scannerOptions;
    private readonly MockUpstoxDataProvider _mockDataProvider;
    private readonly ILogger<UpstoxWebSocketClient> _logger;

    private readonly ConcurrentDictionary<string, string> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Timer? _mockTickTimer;
    private bool _isConnected;
    private readonly Random _random = new();

    public event EventHandler<MarketTickDto>? OnTickReceived;

    public bool IsConnected => _isConnected;
    public IReadOnlyCollection<string> Subscriptions => _subscriptions.Keys.ToList();

    public UpstoxWebSocketClient(
        IOptionsMonitor<UpstoxOptions> upstoxOptions,
        IOptionsMonitor<ScannerBackgroundOptions> scannerOptions,
        MockUpstoxDataProvider mockDataProvider,
        ILogger<UpstoxWebSocketClient> logger)
    {
        _upstoxOptions = upstoxOptions;
        _scannerOptions = scannerOptions;
        _mockDataProvider = mockDataProvider;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
            return;

        _cts = new CancellationTokenSource();
        var options = _upstoxOptions.CurrentValue;

        if (options.IsLiveMode && !string.IsNullOrWhiteSpace(options.AccessToken))
        {
            try
            {
                _logger.LogInformation("Connecting to live Upstox Market Data WebSocket feed...");
                _webSocket = new ClientWebSocket();
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {options.AccessToken}");

                var uri = new Uri(_scannerOptions.CurrentValue.WebSocketUrl);
                await _webSocket.ConnectAsync(uri, cancellationToken);
                _isConnected = true;
                _logger.LogInformation("Connected to Upstox Market Data WebSocket V3 at {Uri}", uri);

                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                // Resubscribe existing subscriptions if any
                if (!_subscriptions.IsEmpty)
                {
                    await SendSubscriptionCommandAsync("sub", _subscriptions.Keys, "full", cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to live Upstox WebSocket feed, falling back to simulated tick feed: {Message}", ex.Message);
                StartMockTickFeed();
            }
        }
        else
        {
            _logger.LogInformation("Starting simulated real-time WebSocket tick feed (Mock Mode)");
            StartMockTickFeed();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _isConnected = false;
        _cts?.Cancel();

        if (_mockTickTimer != null)
        {
            await _mockTickTimer.DisposeAsync();
            _mockTickTimer = null;
        }

        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception closing WebSocket: {Message}", ex.Message);
            }
            finally
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        _logger.LogInformation("Disconnected from Market Data WebSocket feed.");
    }

    public async Task SubscribeAsync(IEnumerable<string> instrumentKeys, string mode = "full", CancellationToken cancellationToken = default)
    {
        var keysList = instrumentKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keysList.Count == 0)
            return;

        foreach (var key in keysList)
        {
            _subscriptions[key] = mode;
        }

        _logger.LogInformation("Subscribed to {Count} instruments on WebSocket feed ({Mode})", keysList.Count, mode);

        if (_isConnected && _webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            await SendSubscriptionCommandAsync("sub", keysList, mode, cancellationToken);
        }
    }

    public async Task UnsubscribeAsync(IEnumerable<string> instrumentKeys, CancellationToken cancellationToken = default)
    {
        var keysList = instrumentKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keysList.Count == 0)
            return;

        foreach (var key in keysList)
        {
            _subscriptions.TryRemove(key, out _);
        }

        _logger.LogInformation("Unsubscribed from {Count} instruments", keysList.Count);

        if (_isConnected && _webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            await SendSubscriptionCommandAsync("unsub", keysList, "full", cancellationToken);
        }
    }

    private async Task SendSubscriptionCommandAsync(string method, IEnumerable<string> keys, string mode, CancellationToken cancellationToken)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            return;

        var command = new
        {
            guid = Guid.NewGuid().ToString("N"),
            method = method,
            data = new
            {
                mode = mode,
                instrumentKeys = keys
            }
        };

        var json = JsonSerializer.Serialize(command);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("Upstox WebSocket server initiated closure: {Desc}", result.CloseStatusDescription);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessTextMessage(message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Upstox WebSocket receive loop: {Message}", ex.Message);
        }
        finally
        {
            _isConnected = false;
        }
    }

    private void ProcessTextMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            // Handle Upstox tick payloads if JSON format received
            if (doc.RootElement.TryGetProperty("feeds", out var feeds) && feeds.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in feeds.EnumerateObject())
                {
                    var key = prop.Name;
                    var feedObj = prop.Value;

                    decimal ltp = 0m;
                    long vol = 0;

                    if (feedObj.TryGetProperty("ltpc", out var ltpc))
                    {
                        if (ltpc.TryGetProperty("ltp", out var ltpProp)) ltp = ltpProp.GetDecimal();
                    }

                    if (feedObj.TryGetProperty("fullFeed", out var fullFeed) &&
                        fullFeed.TryGetProperty("marketFF", out var marketFF) &&
                        marketFF.TryGetProperty("marketOHLC", out var ohlc))
                    {
                        if (marketFF.TryGetProperty("v", out var volProp)) vol = volProp.GetInt64();
                    }

                    if (ltp > 0)
                    {
                        var tick = new MarketTickDto
                        {
                            InstrumentKey = key,
                            LastPrice = ltp,
                            Volume = vol,
                            Timestamp = DateTimeOffset.UtcNow,
                            IsMock = false
                        };

                        OnTickReceived?.Invoke(this, tick);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse incoming WebSocket message: {Message}", ex.Message);
        }
    }

    private void StartMockTickFeed()
    {
        _isConnected = true;
        // Broadcast simulated market ticks every 1.5 seconds for subscribed keys
        _mockTickTimer = new Timer(EmitMockTicks, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1500));
    }

    private void EmitMockTicks(object? state)
    {
        if (!_isConnected || _subscriptions.IsEmpty)
            return;

        var keys = _subscriptions.Keys.ToList();
        var quotes = _mockDataProvider.GetQuotes(keys);

        foreach (var key in keys)
        {
            if (!quotes.TryGetValue(key, out var quote))
                continue;

            // Add slight micro-fluctuation to price (+/- 0.2%)
            decimal jitterPct = (decimal)(_random.NextDouble() * 0.004 - 0.002);
            decimal newPrice = Math.Round(quote.LastPrice * (1m + jitterPct), 2);
            long addedVolume = _random.Next(50, 500);

            var tick = new MarketTickDto
            {
                InstrumentKey = key,
                Symbol = quote.Symbol,
                LastPrice = newPrice,
                Volume = quote.Volume + addedVolume,
                Change = Math.Round(newPrice - quote.PrevClosePrice, 2),
                PercentageChange = quote.PrevClosePrice > 0 ? Math.Round(((newPrice - quote.PrevClosePrice) / quote.PrevClosePrice) * 100m, 2) : 0m,
                Timestamp = DateTimeOffset.UtcNow,
                Open = quote.Open,
                High = Math.Max(quote.High, newPrice),
                Low = Math.Min(quote.Low, newPrice),
                Close = newPrice,
                IsMock = true
            };

            OnTickReceived?.Invoke(this, tick);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _cts?.Dispose();
    }
}
