namespace UpstoxAiScanner.Application.Configuration;

/// <summary>
/// Configuration options for connecting to the Upstox API.
/// </summary>
public class UpstoxOptions
{
    public const string SectionName = "Upstox";

    /// <summary>
    /// Base URL for Upstox API. Default is https://api.upstox.com
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.upstox.com";

    /// <summary>
    /// OAuth Bearer Access Token for Upstox Analytics API.
    /// Loaded securely from .NET User Secrets or environment variables (UPSTOX_ACCESS_TOKEN or Upstox:AccessToken).
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Default API version prefix (e.g., "v3").
    /// </summary>
    public string ApiVersion { get; set; } = "v3";

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum Polly retry attempts for transient errors (429, 503, etc.).
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Max requests allowed per second to stay within Upstox rate limits.
    /// </summary>
    public int ThrottleRequestsPerSecond { get; set; } = 10;

    /// <summary>
    /// When true, automatically falls back to deterministic mock data if no Access Token is configured.
    /// </summary>
    public bool UseMockFallbackWhenUnconfigured { get; set; } = true;

    /// <summary>
    /// Explicitly forces mock mode regardless of whether an access token is present.
    /// </summary>
    public bool ForceMockMode { get; set; } = false;

    /// <summary>
    /// Evaluates if the service should operate in live mode.
    /// </summary>
    public bool IsLiveMode => !ForceMockMode && !string.IsNullOrWhiteSpace(AccessToken);
}
