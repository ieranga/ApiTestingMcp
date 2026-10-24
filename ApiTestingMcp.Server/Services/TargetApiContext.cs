using ApiTestingMcp.Server.Configuration;
using Microsoft.Extensions.Options;

namespace ApiTestingMcp.Server.Services;

/// <summary>
/// Thread-safe context managing the currently configured target API.
/// Allows dynamically switching target APIs at runtime without restarting the server.
/// </summary>
public class TargetApiContext
{
    private readonly object _lock = new();
    private string _baseUrl;
    private string _openApiPath;
    private int _timeoutSeconds;

    public event Action? OnTargetChanged;

    public TargetApiContext(IOptions<TargetApiOptions> defaultOptions)
    {
        _baseUrl = defaultOptions.Value.BaseUrl;
        _openApiPath = defaultOptions.Value.OpenApiPath;
        _timeoutSeconds = defaultOptions.Value.TimeoutSeconds;
    }

    public string BaseUrl
    {
        get { lock (_lock) return _baseUrl; }
    }

    public string OpenApiPath
    {
        get { lock (_lock) return _openApiPath; }
    }

    public int TimeoutSeconds
    {
        get { lock (_lock) return _timeoutSeconds; }
    }

    public void UpdateTarget(string baseUrl, string? openApiPath = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL cannot be empty.", nameof(baseUrl));
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"'{baseUrl}' is not a valid absolute HTTP or HTTPS URL.", nameof(baseUrl));
        }

        // SSRF protection: block cloud metadata and internal link-local addresses
        if (uri.Host.Equals("169.254.169.254", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Access to cloud metadata endpoints is prohibited for security reasons.", nameof(baseUrl));
        }

        var normalizedBase = uri.ToString();
        if (!normalizedBase.EndsWith("/"))
        {
            normalizedBase += "/";
        }

        lock (_lock)
        {
            _baseUrl = normalizedBase;
            if (!string.IsNullOrWhiteSpace(openApiPath))
            {
                var cleanPath = openApiPath.Trim();
                _openApiPath = cleanPath.StartsWith("/") ? cleanPath : "/" + cleanPath;
            }
        }

        OnTargetChanged?.Invoke();
    }
}
