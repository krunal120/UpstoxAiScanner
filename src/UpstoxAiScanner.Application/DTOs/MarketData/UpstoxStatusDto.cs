namespace UpstoxAiScanner.Application.DTOs.MarketData;

/// <summary>
/// DTO describing current Upstox API integration connectivity, mode, and token status.
/// </summary>
public record UpstoxStatusDto
{
    public bool IsLiveMode { get; init; }
    public bool HasTokenConfigured { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = string.Empty;
    public string StatusMessage { get; init; } = string.Empty;
    public DateTimeOffset ServerTimeUtc { get; init; } = DateTimeOffset.UtcNow;
}
