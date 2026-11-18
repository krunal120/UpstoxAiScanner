using System.Text.Json.Serialization;

namespace UpstoxAiScanner.Infrastructure.Upstox.Models;

public class UpstoxApiResponse<T>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<UpstoxApiError>? Errors { get; set; }
}

public class UpstoxApiError
{
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("propertyPath")]
    public string? PropertyPath { get; set; }

    [JsonPropertyName("invalidValue")]
    public object? InvalidValue { get; set; }
}
