namespace UpstoxAiScanner.Application.Configuration;

/// <summary>
/// Configuration options for the AI Technical Explanation Service.
/// Prepares integration with OpenAI API while keeping technical calculations 100% independent.
/// </summary>
public class AiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>
    /// OpenAI API key. Stored in .NET User Secrets or environment variables (OPENAI_API_KEY).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for OpenAI API (default: https://api.openai.com/v1).
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// LLM model name (default: "gpt-4o-mini").
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 25;

    /// <summary>
    /// Temperature for LLM reasoning (0.0 to 1.0). Default: 0.2 for deterministic, factual explanations.
    /// </summary>
    public decimal Temperature { get; set; } = 0.2m;

    /// <summary>
    /// Whether AI explanations are enabled.
    /// </summary>
    public bool EnableAiExplanations { get; set; } = true;

    /// <summary>
    /// Evaluates if live OpenAI API is configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
