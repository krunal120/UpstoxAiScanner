namespace UpstoxAiScanner.Application.Configuration;

/// <summary>
/// Configuration options for the AI Technical Explanation Service.
/// Supports Google Gemini (Pro / Flash) and OpenAI with seamless fallback to deterministic financial engine.
/// </summary>
public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// AI Provider: "Gemini" (recommended) or "OpenAI".
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>
    /// API key for Gemini or OpenAI.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for API (e.g. https://generativelanguage.googleapis.com/v1beta or https://api.openai.com/v1).
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// LLM model name (e.g. "gemini-flash-lite-latest", "gemini-3.8-flash", "gemini-1.5-pro", "gpt-4o-mini").
    /// </summary>
    public string Model { get; set; } = "gemini-flash-lite-latest";

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
    /// Evaluates if live AI API is configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
