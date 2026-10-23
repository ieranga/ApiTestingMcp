using System.ComponentModel;
using ApiTestingMcp.Server.Models;
using ApiTestingMcp.Server.Services;
using ModelContextProtocol.Server;

namespace ApiTestingMcp.Server.Tools;

[McpServerToolType]
public class ApiDiscoveryTools
{
    private readonly OpenApiService _openApiService;

    public ApiDiscoveryTools(OpenApiService openApiService)
    {
        _openApiService = openApiService;
    }

    [McpServerTool(Name = "list_endpoints")]
    [Description("Discovers and lists all endpoints, supported HTTP methods, tags, and summaries exposed by the target API via OpenAPI/Swagger.")]
    public async Task<IReadOnlyList<EndpointSummary>> ListEndpoints(
        [Description("Set to true to bypass cache and re-download the OpenAPI document from the target API")] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        return await _openApiService.GetEndpointsAsync(refresh, cancellationToken);
    }

    [McpServerTool(Name = "get_endpoint_schema")]
    [Description("Retrieves the detailed schema (parameters, request body, response codes, and JSON schemas) for a specific HTTP method and endpoint.")]
    public async Task<EndpointDefinition?> GetEndpointSchema(
        [Description("HTTP method (e.g. GET, POST, PUT, DELETE)")] string method,
        [Description("Relative API endpoint path (e.g. '/api/users' or '/api/users/{id}')")] string endpoint,
        CancellationToken cancellationToken = default)
    {
        return await _openApiService.GetEndpointSchemaAsync(method, endpoint, cancellationToken);
    }
}
