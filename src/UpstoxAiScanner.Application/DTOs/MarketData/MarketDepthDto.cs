namespace UpstoxAiScanner.Application.DTOs.MarketData;

public record MarketDepthItemDto
{
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public int Orders { get; init; }
}

public record MarketDepthDto
{
    public IReadOnlyList<MarketDepthItemDto> Buy { get; init; } = Array.Empty<MarketDepthItemDto>();
    public IReadOnlyList<MarketDepthItemDto> Sell { get; init; } = Array.Empty<MarketDepthItemDto>();
}
