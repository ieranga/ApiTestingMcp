using System.ComponentModel;
using ApiTestingMcp.Server.Models;
using ApiTestingMcp.Server.Services;
using ModelContextProtocol.Server;

namespace ApiTestingMcp.Server.Tools;

[McpServerToolType]
public class ApiTestingTools
{
    private readonly ApiClientService _apiClient;
    private readonly TargetApiContext _targetContext;

    public ApiTestingTools(ApiClientService apiClient, TargetApiContext targetContext)
    {
        _apiClient = apiClient;
        _targetContext = targetContext;
    }

    [McpServerTool(Name = "set_target_api")]
    [Description("Configures or switches the target API URL and Swagger/OpenAPI path for testing. Allows testing any live public API or local API via ngrok.")]
    public object SetTargetApi(
        [Description("The base URL of the target API to test (e.g., 'https://petstore.swagger.io' or 'https://api.mycompany.com')")] string baseUrl,
        [Description("Optional path to the OpenAPI/Swagger JSON endpoint (default: '/swagger/v1/swagger.json')")] string? openApiPath = null)
    {
        try
        {
            _targetContext.UpdateTarget(baseUrl, openApiPath);
            return new
            {
                success = true,
                targetBaseUrl = _targetContext.BaseUrl,
                openApiPath = _targetContext.OpenApiPath,
                message = $"Target API successfully updated to '{_targetContext.BaseUrl}'. You can now run check_api_health, list_endpoints, or run_test_suite."
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    [McpServerTool(Name = "get_target_api")]
    [Description("Returns the currently configured target API base URL and OpenAPI schema path.")]
    public object GetTargetApi()
    {
        return new
        {
            targetBaseUrl = _targetContext.BaseUrl,
            openApiPath = _targetContext.OpenApiPath,
            timeoutSeconds = _targetContext.TimeoutSeconds
        };
    }

    [McpServerTool(Name = "check_api_health")]
    [Description("Checks whether the configured target API base URL (or an optionally specified URL) is online, reachable, and accepting connections.")]
    public async Task<ApiHealthStatus> CheckApiHealth(
        [Description("Optional target URL to check. If omitted, checks the currently configured target API.")] string? targetUrl = null,
        CancellationToken cancellationToken = default)
    {
        return await _apiClient.CheckHealthAsync(targetUrl, cancellationToken);
    }

    [McpServerTool(Name = "call_api")]
    [Description("Executes an HTTP request against the configured target API and returns status code, headers, URL, and body.")]
    public async Task<ApiResponse> CallApi(
        [Description("HTTP method to use (GET, POST, PUT, PATCH, DELETE, etc.)")] string method,
        [Description("Relative path of the target API endpoint (e.g. '/api/users/10')")] string endpoint,
        [Description("Optional request body string (typically JSON)")] string? body = null,
        [Description("Optional custom HTTP headers")] Dictionary<string, string>? headers = null,
        [Description("Optional query parameters")] Dictionary<string, string>? queryParameters = null,
        [Description("Whether to attach stored authentication token/headers. Default is true.")] bool useAuth = true,
        [Description("Optional authentication profile name to use. Defaults to active profile.")] string? authProfile = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ApiRequest
        {
            Method = method,
            Endpoint = endpoint,
            Body = body,
            Headers = headers,
            QueryParameters = queryParameters,
            UseAuth = useAuth,
            AuthProfile = authProfile
        };

        return await _apiClient.SendAsync(request, cancellationToken);
    }
}
