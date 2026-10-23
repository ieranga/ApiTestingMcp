using System.ComponentModel;
using ApiTestingMcp.Server.Models;
using ApiTestingMcp.Server.Services;
using ModelContextProtocol.Server;

namespace ApiTestingMcp.Server.Tools;

[McpServerToolType]
public class ApiTestingTools
{
    private readonly ApiClientService _apiClient;

    public ApiTestingTools(ApiClientService apiClient)
    {
        _apiClient = apiClient;
    }

    [McpServerTool(Name = "check_api_health")]
    [Description("Checks whether the configured target API base URL is online, reachable, and accepting connections.")]
    public async Task<ApiHealthStatus> CheckApiHealth(CancellationToken cancellationToken = default)
    {
        return await _apiClient.CheckHealthAsync(cancellationToken);
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
