namespace ApiTestingMcp.Server.Models;

public class ApiHealthStatus
{
    public bool IsOnline { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }

    public static ApiHealthStatus Online(string baseUrl, int statusCode, long responseTimeMs, string message = "Target API is online and accepting connections.")
    {
        return new ApiHealthStatus
        {
            IsOnline = true,
            BaseUrl = baseUrl,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            Message = message
        };
    }

    public static ApiHealthStatus Offline(string baseUrl, string message, string? errorDetails = null)
    {
        return new ApiHealthStatus
        {
            IsOnline = false,
            BaseUrl = baseUrl,
            StatusCode = null,
            ResponseTimeMs = 0,
            Message = message,
            ErrorDetails = errorDetails
        };
    }
}
