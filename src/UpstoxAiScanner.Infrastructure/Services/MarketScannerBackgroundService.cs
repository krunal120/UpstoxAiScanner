using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpstoxAiScanner.Application.Configuration;
using UpstoxAiScanner.Application.DTOs.Scanner;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Infrastructure.Services;

/// <summary>
/// Hosted background service that executes scheduled scans during Indian stock market hours,
/// monitors high-probability technical setups, and coordinates real-time WebSocket feeds.
/// </summary>
public class MarketScannerBackgroundService : BackgroundService, IBackgroundScannerControllerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMarketHoursService _marketHoursService;
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly IOptionsMonitor<ScannerBackgroundOptions> _optionsMonitor;
    private readonly ILogger<MarketScannerBackgroundService> _logger;

    private bool _isEnabled;
    private bool _ignoreMarketHours;
    private DateTimeOffset? _lastScanTime;
    private DateTimeOffset? _nextScheduledScanTime;
    private int _totalScansExecuted;
    private IReadOnlyList<ScanResultDto> _latestTopSignals = Array.Empty<ScanResultDto>();

    public MarketScannerBackgroundService(
        IServiceScopeFactory scopeFactory,
        IMarketHoursService marketHoursService,
        IUpstoxWebSocketClient webSocketClient,
        IOptionsMonitor<ScannerBackgroundOptions> optionsMonitor,
        ILogger<MarketScannerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _marketHoursService = marketHoursService;
        _webSocketClient = webSocketClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        _isEnabled = _optionsMonitor.CurrentValue.EnableBackgroundScanning;
        _ignoreMarketHours = _optionsMonitor.CurrentValue.IgnoreMarketHours;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketScannerBackgroundService starting up...");

        // Connect WebSocket client if streaming enabled
        if (_optionsMonitor.CurrentValue.EnableWebSocketStreaming)
        {
            try
            {
                await _webSocketClient.ConnectAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect WebSocket client on startup: {Message}", ex.Message);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var isMarketOpen = _marketHoursService.IsMarketOpen();
            bool shouldScan = _isEnabled && (isMarketOpen || _ignoreMarketHours);

            if (shouldScan)
            {
                _logger.LogInformation("Initiating automated background intraday scan for {Universe}...", options.Universe);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scannerService = scope.ServiceProvider.GetRequiredService<IScannerService>();

                    var topResults = await scannerService.RunScanAsync(
                        "Intraday",
                        options.Universe,
                        10,
                        stoppingToken);

                    _latestTopSignals = topResults;
                    _lastScanTime = DateTimeOffset.UtcNow;
                    _totalScansExecuted++;

                    // Log strong setups
                    var highConviction = topResults.Where(r => r.TechnicalScore >= 80m || r.TechnicalScore <= 20m).ToList();
                    if (highConviction.Count > 0)
                    {
                        _logger.LogInformation("Background scan detected {Count} high-conviction setups: {Symbols}",
                            highConviction.Count,
                            string.Join(", ", highConviction.Select(r => $"{r.Symbol}: {r.TechnicalScore} ({r.Signal})")));
                    }

                    // Auto-subscribe top opportunities to WebSocket feed for real-time tick streaming
                    if (_webSocketClient.IsConnected && topResults.Count > 0)
                    {
                        var topKeys = topResults.Select(r => r.InstrumentKey).ToList();
                        await _webSocketClient.SubscribeAsync(topKeys, "full", stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error during background scan cycle: {Message}", ex.Message);
                }
            }
            else
            {
                var session = _marketHoursService.GetSessionStatus();
                _logger.LogDebug("Background scanner paused ({Phase}). Market Open: {IsOpen}, Scanning Enabled: {Enabled}",
                    session.SessionPhase, isMarketOpen, _isEnabled);
            }

            int delaySeconds = Math.Max(10, options.IntradayScanIntervalSeconds);
            _nextScheduledScanTime = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("MarketScannerBackgroundService stopped.");
    }

    public BackgroundScannerStatusDto GetStatus()
    {
        var session = _marketHoursService.GetSessionStatus();
        return new BackgroundScannerStatusDto
        {
            IsEnabled = _isEnabled,
            IsMarketOpen = session.IsMarketOpen,
            IgnoreMarketHours = _ignoreMarketHours,
            MarketSessionPhase = session.SessionPhase,
            LastScanTimeUtc = _lastScanTime,
            NextScheduledScanTimeUtc = _nextScheduledScanTime,
            TotalScansExecuted = _totalScansExecuted,
            IntradayScanIntervalSeconds = _optionsMonitor.CurrentValue.IntradayScanIntervalSeconds,
            Universe = _optionsMonitor.CurrentValue.Universe,
            IsWebSocketConnected = _webSocketClient.IsConnected,
            WebSocketActiveSubscriptionsCount = _webSocketClient.Subscriptions.Count,
            LatestTopSignals = _latestTopSignals
        };
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        _logger.LogInformation("Background scanning state updated: Enabled = {Enabled}", enabled);
    }

    public void SetIgnoreMarketHours(bool ignore)
    {
        _ignoreMarketHours = ignore;
        _logger.LogInformation("Background scanner IgnoreMarketHours updated: {Ignore}", ignore);
    }
}
