using UpstoxAiScanner.Application.DTOs.MarketData;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Client contract for connecting to Upstox Market Data WebSocket V3 feed, managing subscriptions,
/// and streaming live market ticks.
/// </summary>
public interface IUpstoxWebSocketClient : IAsyncDisposable
{
    /// <summary>
    /// Whether the client is currently connected to the feed.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Currently subscribed instrument keys.
    /// </summary>
    IReadOnlyCollection<string> Subscriptions { get; }

    /// <summary>
    /// Event triggered when a new market tick is received.
    /// </summary>
    event EventHandler<MarketTickDto>? OnTickReceived;

    /// <summary>
    /// Initiates connection to the Upstox Market Data WebSocket feed.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the WebSocket feed.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to one or more instrument keys for real-time market data ticks.
    /// </summary>
    /// <param name="instrumentKeys">Instrument keys (e.g. "NSE_EQ|INE002A01018").</param>
    /// <param name="mode">Subscription mode: "full" or "ltpc". Default: "full".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubscribeAsync(IEnumerable<string> instrumentKeys, string mode = "full", CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from one or more instrument keys.
    /// </summary>
    Task UnsubscribeAsync(IEnumerable<string> instrumentKeys, CancellationToken cancellationToken = default);
}
