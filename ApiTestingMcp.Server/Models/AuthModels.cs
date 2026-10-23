namespace ApiTestingMcp.Server.Models;

public class AuthStatus
{
    public bool IsAuthenticated { get; set; }
    public string ActiveProfile { get; set; } = "default";
    public string AuthType { get; set; } = "None";
    public string? TokenPreview { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Dictionary<string, string> Claims { get; set; } = [];
    public List<string> AvailableProfiles { get; set; } = [];
}

public class LoginResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Profile { get; set; }
    public string? TokenPreview { get; set; }
    public Dictionary<string, string>? Claims { get; set; }
    public string? RawResponseBody { get; set; }
}
