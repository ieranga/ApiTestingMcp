using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using ApiTestingMcp.Server.Configuration;
using ApiTestingMcp.Server.Models;
using Microsoft.Extensions.Options;

namespace ApiTestingMcp.Server.Services;

public class ApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly TargetApiContext _targetContext;
    private readonly AuthManagerService _authManager;
    private readonly ILogger<ApiClientService> _logger;

    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
    };

    public ApiClientService(
        HttpClient httpClient,
        TargetApiContext targetContext,
        AuthManagerService authManager,
        ILogger<ApiClientService> logger)
    {
        _httpClient = httpClient;
        _targetContext = targetContext;
        _authManager = authManager;
        _logger = logger;

        if (_targetContext.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_targetContext.TimeoutSeconds);
        }
    }

    public async Task<ApiHealthStatus> CheckHealthAsync(string? overrideUrl = null, CancellationToken cancellationToken = default)
    {
        var targetUrl = !string.IsNullOrWhiteSpace(overrideUrl) ? overrideUrl.Trim() : _targetContext.BaseUrl;
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return ApiHealthStatus.Offline(targetUrl, $"Target URL '{targetUrl}' is not a valid absolute HTTP/HTTPS URL.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var probeTimeout = Math.Max(_targetContext.TimeoutSeconds > 0 ? _targetContext.TimeoutSeconds : 15, 15);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(probeTimeout));

            using var request = new HttpRequestMessage(HttpMethod.Get, baseUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            stopwatch.Stop();

            return ApiHealthStatus.Online(
                baseUri.ToString(),
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                $"Target API at '{baseUri}' is ONLINE (responded with HTTP {(int)response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms).");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException sockEx)
        {
            stopwatch.Stop();
            return ApiHealthStatus.Offline(
                baseUri.ToString(),
                $"Target API at '{baseUri}' is OFFLINE: Connection refused ({sockEx.Message}). Ensure the target service is started.",
                sockEx.Message);
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return ApiHealthStatus.Offline(
                baseUri.ToString(),
                $"Target API at '{baseUri}' is OFFLINE or UNRESPONSIVE: Connection timed out.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ApiHealthStatus.Offline(
                baseUri.ToString(),
                $"Target API at '{baseUri}' check failed: {ex.Message}",
                ex.ToString());
        }
    }

    public async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            _logger.LogWarning("API request validation failed: {Error}", validationError);
            return ApiResponse.Failure(validationError);
        }

        var normalizedMethod = request.Method.Trim().ToUpperInvariant();
        var normalizedEndpoint = request.Endpoint.Trim();

        // Construct target URI
        var uriBuilder = new StringBuilder();
        if (!normalizedEndpoint.StartsWith("/"))
        {
            uriBuilder.Append('/');
        }
        uriBuilder.Append(normalizedEndpoint);

        if (request.QueryParameters != null && request.QueryParameters.Count > 0)
        {
            var separator = normalizedEndpoint.Contains('?') ? '&' : '?';
            foreach (var kvp in request.QueryParameters)
            {
                uriBuilder.Append(separator);
                uriBuilder.Append(Uri.EscapeDataString(kvp.Key));
                uriBuilder.Append('=');
                uriBuilder.Append(Uri.EscapeDataString(kvp.Value ?? string.Empty));
                separator = '&';
            }
        }

        var relativePathAndQuery = uriBuilder.ToString().TrimStart('/');
        var baseUri = new Uri(_targetContext.BaseUrl);
        var targetUri = new Uri(baseUri, relativePathAndQuery);

        // Security check: Ensure target URL remains within configured BaseAddress origin
        if (targetUri.Host != baseUri.Host ||
            targetUri.Port != baseUri.Port ||
            targetUri.Scheme != baseUri.Scheme)
        {
            return ApiResponse.Failure($"Target URL '{targetUri}' escapes the configured base origin '{baseUri}'.");
        }

        var httpRequest = new HttpRequestMessage(new HttpMethod(normalizedMethod), targetUri);

        // Set request body if applicable
        if (!string.IsNullOrEmpty(request.Body))
        {
            if (normalizedMethod is "GET" or "HEAD" or "OPTIONS")
            {
                _logger.LogInformation("Request body provided for {Method} request.", normalizedMethod);
            }

            var content = new StringContent(request.Body, Encoding.UTF8, "application/json");
            httpRequest.Content = content;
        }

        // Apply headers
        if (request.Headers != null)
        {
            foreach (var (key, value) in request.Headers)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;

                // Handle content headers if content exists
                if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) && httpRequest.Content != null)
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation(key, value);
                }
                else
                {
                    httpRequest.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }

        // Apply authentication if requested
        if (request.UseAuth)
        {
            _authManager.ApplyAuthHeaders(httpRequest, request.AuthProfile);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in response.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            return new ApiResponse
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase ?? response.StatusCode.ToString(),
                FinalUrl = response.RequestMessage?.RequestUri?.ToString() ?? targetUri.ToString(),
                Headers = headers,
                Body = responseBody,
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP request to {Url} timed out.", targetUri);
            return ApiResponse.Failure($"Request timed out after {_targetContext.TimeoutSeconds} seconds.", targetUri.ToString());
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP request failed for {Url}: {Message}", targetUri, ex.Message);

            if (ex.InnerException is System.Net.Sockets.SocketException sockEx)
            {
                return ApiResponse.Failure(
                    $"Target API is OFFLINE: Unable to connect to '{baseUri}' ({sockEx.Message}). Ensure the target API service is started and listening.",
                    targetUri.ToString());
            }

            return ApiResponse.Failure($"HTTP request error: {ex.Message}", targetUri.ToString(), (int?)ex.StatusCode ?? 0);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error calling {Url}: {Message}", targetUri, ex.Message);
            return ApiResponse.Failure($"Unexpected error: {ex.Message}", targetUri.ToString());
        }
    }

    private static string? ValidateRequest(ApiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Method))
        {
            return "HTTP method is required.";
        }

        var method = request.Method.Trim();
        if (!AllowedMethods.Contains(method))
        {
            return $"Unsupported HTTP method '{method}'. Allowed methods: {string.Join(", ", AllowedMethods)}.";
        }

        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return "Endpoint is required.";
        }

        var endpoint = request.Endpoint.Trim();

        // Reject absolute URLs
        if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            endpoint.StartsWith("//") ||
            (Uri.TryCreate(endpoint, UriKind.Absolute, out var parsedAbsolute) && !string.IsNullOrEmpty(parsedAbsolute.Scheme)))
        {
            return "Absolute or protocol-relative URLs are not permitted. Only relative paths (e.g. '/api/users') are accepted.";
        }

        // Reject path traversal
        if (endpoint.Contains("/../") || endpoint.EndsWith("/..") || endpoint.StartsWith("../") || endpoint == "..")
        {
            return "Path traversal segments ('..') are not permitted in endpoint.";
        }

        return null;
    }
}
