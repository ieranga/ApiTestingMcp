using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ApiTestingMcp.Server.Configuration;
using ApiTestingMcp.Server.Models;
using Microsoft.Extensions.Options;

namespace ApiTestingMcp.Server.Services;

public class AuthProfileData
{
    public string? BearerToken { get; set; }
    public string? ApiKeyHeader { get; set; }
    public string? ApiKeyValue { get; set; }
    public Dictionary<string, string> CustomHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AuthManagerService
{
    private readonly ConcurrentDictionary<string, AuthProfileData> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private string _activeProfile = "default";
    private readonly ILogger<AuthManagerService> _logger;

    public AuthManagerService(IOptions<TargetApiOptions> options, ILogger<AuthManagerService> logger)
    {
        _logger = logger;
        _profiles["default"] = new AuthProfileData();

        // Check if an initial token or API key is set via environment variable
        var envToken = Environment.GetEnvironmentVariable("TARGET_API_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            SetBearerToken(envToken.Trim(), "default");
            _logger.LogInformation("Loaded default Bearer token from environment variable TARGET_API_TOKEN.");
        }

        var envApiKey = Environment.GetEnvironmentVariable("TARGET_API_KEY");
        var envApiHeader = Environment.GetEnvironmentVariable("TARGET_API_KEY_HEADER") ?? "X-Api-Key";
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            SetApiKey(envApiHeader, envApiKey.Trim(), "default");
            _logger.LogInformation("Loaded default API Key from environment variable TARGET_API_KEY.");
        }
    }

    public void SetActiveProfile(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile)) return;
        _activeProfile = profile.Trim();
        _profiles.GetOrAdd(_activeProfile, _ => new AuthProfileData());
    }

    public string GetActiveProfile() => _activeProfile;

    public void SetBearerToken(string token, string? profile = null)
    {
        var targetProfile = string.IsNullOrWhiteSpace(profile) ? _activeProfile : profile.Trim();
        var data = _profiles.GetOrAdd(targetProfile, _ => new AuthProfileData());
        data.BearerToken = token.Trim().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token.Trim()[7..].Trim()
            : token.Trim();

        _logger.LogInformation("Bearer token set for profile '{Profile}'.", targetProfile);
    }

    public void SetApiKey(string headerName, string apiKey, string? profile = null)
    {
        var targetProfile = string.IsNullOrWhiteSpace(profile) ? _activeProfile : profile.Trim();
        var data = _profiles.GetOrAdd(targetProfile, _ => new AuthProfileData());
        data.ApiKeyHeader = headerName.Trim();
        data.ApiKeyValue = apiKey.Trim();

        _logger.LogInformation("API Key header '{Header}' set for profile '{Profile}'.", headerName, targetProfile);
    }

    public void SetCustomHeader(string name, string value, string? profile = null)
    {
        var targetProfile = string.IsNullOrWhiteSpace(profile) ? _activeProfile : profile.Trim();
        var data = _profiles.GetOrAdd(targetProfile, _ => new AuthProfileData());
        data.CustomHeaders[name.Trim()] = value.Trim();
    }

    public void ClearAuth(string? profile = null)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            if (_profiles.TryGetValue(_activeProfile, out var activeData))
            {
                activeData.BearerToken = null;
                activeData.ApiKeyHeader = null;
                activeData.ApiKeyValue = null;
                activeData.CustomHeaders.Clear();
            }
        }
        else if (_profiles.TryGetValue(profile.Trim(), out var pData))
        {
            pData.BearerToken = null;
            pData.ApiKeyHeader = null;
            pData.ApiKeyValue = null;
            pData.CustomHeaders.Clear();
        }
    }

    public void ApplyAuthHeaders(HttpRequestMessage request, string? profile = null)
    {
        var targetProfile = string.IsNullOrWhiteSpace(profile) ? _activeProfile : profile.Trim();
        if (!_profiles.TryGetValue(targetProfile, out var data))
        {
            return;
        }

        // Apply Bearer token if not already in request
        if (!string.IsNullOrEmpty(data.BearerToken) && !request.Headers.Contains("Authorization"))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {data.BearerToken}");
        }

        // Apply API Key if configured
        if (!string.IsNullOrEmpty(data.ApiKeyHeader) && !string.IsNullOrEmpty(data.ApiKeyValue) &&
            !request.Headers.Contains(data.ApiKeyHeader))
        {
            request.Headers.TryAddWithoutValidation(data.ApiKeyHeader, data.ApiKeyValue);
        }

        // Apply Custom Headers
        foreach (var (k, v) in data.CustomHeaders)
        {
            if (!request.Headers.Contains(k))
            {
                request.Headers.TryAddWithoutValidation(k, v);
            }
        }
    }

    public AuthStatus GetStatus(string? profile = null)
    {
        var targetProfile = string.IsNullOrWhiteSpace(profile) ? _activeProfile : profile.Trim();
        var hasProfile = _profiles.TryGetValue(targetProfile, out var data);

        var status = new AuthStatus
        {
            ActiveProfile = targetProfile,
            AvailableProfiles = _profiles.Keys.ToList()
        };

        if (!hasProfile || data == null)
        {
            status.IsAuthenticated = false;
            return status;
        }

        if (!string.IsNullOrEmpty(data.BearerToken))
        {
            status.IsAuthenticated = true;
            status.AuthType = "Bearer";
            status.TokenPreview = MaskToken(data.BearerToken);

            var (claims, expiresAt) = DecodeJwtClaims(data.BearerToken);
            status.Claims = claims;
            status.ExpiresAt = expiresAt;
        }
        else if (!string.IsNullOrEmpty(data.ApiKeyHeader))
        {
            status.IsAuthenticated = true;
            status.AuthType = $"ApiKey ({data.ApiKeyHeader})";
            status.TokenPreview = MaskToken(data.ApiKeyValue ?? "");
        }
        else if (data.CustomHeaders.Count > 0)
        {
            status.IsAuthenticated = true;
            status.AuthType = "CustomHeaders";
        }
        else
        {
            status.IsAuthenticated = false;
        }

        return status;
    }

    public static string? TryExtractToken(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            return FindTokenInJson(root);
        }
        catch
        {
            // Response might be a plain token string
            var trimmed = responseBody.Trim().Trim('"');
            if (trimmed.StartsWith("ey") && trimmed.Count(c => c == '.') == 2)
            {
                return trimmed;
            }
            return null;
        }
    }

    private static string? FindTokenInJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        // Common top-level token fields
        string[] candidates = ["token", "accessToken", "access_token", "jwt", "id_token", "idToken", "bearerToken"];

        foreach (var key in candidates)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }
        }

        // Nested in 'data' or 'result' object
        string[] wrapperCandidates = ["data", "result", "payload", "auth", "response"];
        foreach (var wrapper in wrapperCandidates)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, wrapper, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        var nested = FindTokenInJson(prop.Value);
                        if (nested != null) return nested;
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.Value.GetString();
                        if (!string.IsNullOrEmpty(val) && val.StartsWith("ey") && val.Count(c => c == '.') == 2)
                        {
                            return val;
                        }
                    }
                }
            }
        }

        return null;
    }

    public static (Dictionary<string, string> Claims, DateTimeOffset? ExpiresAt) DecodeJwtClaims(string jwt)
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DateTimeOffset? expiresAt = null;

        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return (claims, null);

            var payloadBase64 = parts[1];
            // Fix base64url padding
            payloadBase64 = payloadBase64.Replace('-', '+').Replace('_', '/');
            switch (payloadBase64.Length % 4)
            {
                case 2: payloadBase64 += "=="; break;
                case 3: payloadBase64 += "="; break;
            }

            var bytes = Convert.FromBase64String(payloadBase64);
            var json = Encoding.UTF8.GetString(bytes);

            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var val = prop.Value.ToString();
                claims[prop.Name] = val;

                if (string.Equals(prop.Name, "exp", StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.TryGetInt64(out var expSeconds))
                {
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                }
            }
        }
        catch
        {
            // Ignore JWT parsing errors (e.g. non-JWT tokens or opaque tokens)
        }

        return (claims, expiresAt);
    }

    public static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return "";
        if (token.Length <= 10) return "****";
        return $"{token[..6]}...{token[^4..]}";
    }
}
