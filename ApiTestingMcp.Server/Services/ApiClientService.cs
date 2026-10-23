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
    private readonly TargetApiOptions _options;
    private readonly AuthManagerService _authManager;
    private readonly ILogger<ApiClientService> _logger;

    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
    };

    public ApiClientService(
        HttpClient httpClient,
        IOptions<TargetApiOptions> options,
        AuthManagerService authManager,
        ILogger<ApiClientService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _authManager = authManager;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("TargetApi:BaseUrl is not configured.");
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"TargetApi:BaseUrl '{_options.BaseUrl}' is not a valid absolute HTTP/HTTPS URL.");
        }

        _httpClient.BaseAddress = baseUri;
        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task<ApiHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var baseUri = _httpClient.BaseAddress;
        if (baseUri == null)
        {
            return ApiHealthStatus.Offline(_options.BaseUrl, "TargetApi:BaseUrl is not configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Min(_options.TimeoutSeconds, 5)));

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

        var relativePathAndQuery = uriBuilder.ToString();
        var targetUri = new Uri(_httpClient.BaseAddress!, relativePathAndQuery);

        // Security check: Ensure target URL remains within configured BaseAddress origin
        if (targetUri.Host != _httpClient.BaseAddress!.Host ||
            targetUri.Port != _httpClient.BaseAddress.Port ||
            targetUri.Scheme != _httpClient.BaseAddress.Scheme)
        {
            return ApiResponse.Failure($"Target URL '{targetUri}' escapes the configured base origin '{_httpClient.BaseAddress}'.");
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
            return ApiResponse.Failure($"Request timed out after {_options.TimeoutSeconds} seconds.", targetUri.ToString());
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP request failed for {Url}: {Message}", targetUri, ex.Message);

            if (ex.InnerException is System.Net.Sockets.SocketException sockEx)
            {
                return ApiResponse.Failure(
                    $"Target API is OFFLINE: Unable to connect to '{_httpClient.BaseAddress}' ({sockEx.Message}). Ensure the target API service is started and listening.",
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
