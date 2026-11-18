namespace UpstoxAiScanner.Domain.Models;

/// <summary>
/// Individual price tier in market depth (bids/asks).
/// </summary>
public record MarketDepthItem
{
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public int Orders { get; init; }
}

/// <summary>
/// 5-tier market depth order book (buy and sell sides).
/// </summary>
public record MarketDepth
{
    public IReadOnlyList<MarketDepthItem> Buy { get; init; } = Array.Empty<MarketDepthItem>();
    public IReadOnlyList<MarketDepthItem> Sell { get; init; } = Array.Empty<MarketDepthItem>();
}
