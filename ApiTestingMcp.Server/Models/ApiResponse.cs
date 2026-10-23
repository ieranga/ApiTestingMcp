namespace ApiTestingMcp.Server.Models;

public class ApiResponse
{
    public int StatusCode { get; set; }
    public string ReasonPhrase { get; set; } = string.Empty;
    public string FinalUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Body { get; set; }
    public bool IsSuccessStatusCode { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public string? ErrorMessage { get; set; }

    public static ApiResponse Failure(string errorMessage, string finalUrl = "", int statusCode = 0)
    {
        return new ApiResponse
        {
            StatusCode = statusCode,
            ReasonPhrase = "Client Error / Validation Failure",
            FinalUrl = finalUrl,
            IsSuccessStatusCode = false,
            ErrorMessage = errorMessage
        };
    }
}
