namespace UpstoxAiScanner.Application.Common;

/// <summary>
/// Standard API response envelope providing structured response payload, status, and mock data indicator.
/// </summary>
/// <typeparam name="T">Payload type</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public bool IsMockData { get; set; }
    public DateTimeOffset ServerTimeUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ErrorCode { get; set; }

    public static ApiResponse<T> Ok(T data, bool isMockData = false, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            IsMockData = isMockData,
            Message = message,
            ServerTimeUtc = DateTimeOffset.UtcNow
        };
    }

    public static ApiResponse<T> Fail(string message, string? errorCode = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            ErrorCode = errorCode,
            ServerTimeUtc = DateTimeOffset.UtcNow
        };
    }
}
