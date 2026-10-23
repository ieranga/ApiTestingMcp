using System.ComponentModel;
using ApiTestingMcp.Server.Models;
using ApiTestingMcp.Server.Services;
using ModelContextProtocol.Server;

namespace ApiTestingMcp.Server.Tools;

[McpServerToolType]
public class ApiValidationTools
{
    private readonly ResponseValidatorService _validator;
    private readonly ApiClientService _apiClient;
    private readonly OpenApiService _openApiService;

    public ApiValidationTools(
        ResponseValidatorService validator,
        ApiClientService apiClient,
        OpenApiService openApiService)
    {
        _validator = validator;
        _apiClient = apiClient;
        _openApiService = openApiService;
    }

    [McpServerTool(Name = "validate_response")]
    [Description("Validates an API response against expected status code, required JSON fields, data types, header values, and value assertions.")]
    public ValidationReport ValidateResponse(
        [Description("Actual HTTP status code received (e.g. 200, 404, 401)")] int statusCode,
        [Description("Response body string to validate (typically JSON)")] string? body = null,
        [Description("Expected HTTP status code to assert (e.g. 200, 201)")] int? expectedStatus = null,
        [Description("List of required JSON property paths (supports dot-notation, e.g. ['success', 'data.id'])")] List<string>? requiredFields = null,
        [Description("Dictionary of expected field types, e.g. {'success': 'boolean', 'data': 'array'}")] Dictionary<string, string>? fieldTypes = null,
        [Description("Dictionary of value assertions, e.g. {'success': 'true', 'data': 'not_empty'}")] Dictionary<string, string>? valueAssertions = null,
        [Description("Dictionary of expected headers and substrings, e.g. {'Content-Type': 'application/json'}")] Dictionary<string, string>? expectedHeaders = null,
        [Description("Maximum allowed latency SLA in milliseconds (optional)")] long? maxLatencyMs = null,
        [Description("Response headers dictionary (optional)")] Dictionary<string, string>? headers = null,
        [Description("Elapsed response time in milliseconds (optional)")] long elapsedMilliseconds = 0)
    {
        var rules = new ValidationRule
        {
            ExpectedStatusCode = expectedStatus,
            RequiredFields = requiredFields,
            FieldTypes = fieldTypes,
            ValueAssertions = valueAssertions,
            ExpectedHeaders = expectedHeaders,
            MaxLatencyMs = maxLatencyMs
        };

        return _validator.Validate(statusCode, body, headers, elapsedMilliseconds, rules);
    }

    [McpServerTool(Name = "call_and_validate")]
    [Description("Executes an HTTP API call and immediately validates the response in a single step, returning both the response and the PASS/FAIL validation report.")]
    public async Task<CallAndValidateResult> CallAndValidate(
        [Description("HTTP method (GET, POST, PUT, DELETE, etc.)")] string method,
        [Description("Relative endpoint path (e.g. '/api/Auth/users')")] string endpoint,
        [Description("Expected HTTP status code to assert (e.g. 200, 401)")] int expectedStatus = 200,
        [Description("List of required JSON fields (e.g. ['success', 'data'])")] List<string>? requiredFields = null,
        [Description("Field type expectations (e.g. {'success': 'boolean', 'data': 'array'})")] Dictionary<string, string>? fieldTypes = null,
        [Description("Value assertions (e.g. {'success': 'true'})")] Dictionary<string, string>? valueAssertions = null,
        [Description("Optional JSON request body")] string? requestBody = null,
        [Description("Optional custom HTTP headers")] Dictionary<string, string>? requestHeaders = null,
        [Description("Whether to attach authentication. Default is true.")] bool useAuth = true,
        [Description("Optional auth profile name. Defaults to active profile.")] string? authProfile = null,
        [Description("Maximum allowed latency SLA in milliseconds")] long? maxLatencyMs = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ApiRequest
        {
            Method = method,
            Endpoint = endpoint,
            Body = requestBody,
            Headers = requestHeaders,
            UseAuth = useAuth,
            AuthProfile = authProfile
        };

        var response = await _apiClient.SendAsync(request, cancellationToken);

        var rules = new ValidationRule
        {
            ExpectedStatusCode = expectedStatus,
            RequiredFields = requiredFields,
            FieldTypes = fieldTypes,
            ValueAssertions = valueAssertions,
            MaxLatencyMs = maxLatencyMs
        };

        var validationReport = _validator.Validate(
            response.StatusCode,
            response.Body,
            response.Headers,
            response.ElapsedMilliseconds,
            rules);

        return new CallAndValidateResult
        {
            Response = response,
            Validation = validationReport
        };
    }

    [McpServerTool(Name = "validate_against_schema")]
    [Description("Validates an actual API response against the documented contract in OpenAPI/Swagger for that endpoint.")]
    public async Task<ValidationReport> ValidateAgainstSchema(
        [Description("HTTP method of the endpoint (e.g. 'GET', 'POST')")] string method,
        [Description("Relative path of the endpoint (e.g. '/api/Auth/users')")] string endpoint,
        [Description("Actual HTTP status code received")] int statusCode,
        [Description("Response body string to validate")] string? body = null,
        [Description("Response headers dictionary")] Dictionary<string, string>? headers = null,
        [Description("Elapsed time in milliseconds")] long elapsedMilliseconds = 0,
        CancellationToken cancellationToken = default)
    {
        var schema = await _openApiService.GetEndpointSchemaAsync(method, endpoint, cancellationToken);
        if (schema == null)
        {
            return new ValidationReport
            {
                IsPassed = false,
                TotalAssertions = 1,
                FailedCount = 1,
                Summary = $"FAIL: Endpoint '{method} {endpoint}' not found in OpenAPI specification.",
                Assertions =
                [
                    new AssertionResult
                    {
                        Name = "Schema Lookup",
                        Passed = false,
                        Expected = "Endpoint defined in OpenAPI",
                        Actual = "Not found",
                        ErrorMessage = $"No OpenAPI schema found matching {method} {endpoint}."
                    }
                ]
            };
        }

        var fakeResponse = new ApiResponse
        {
            StatusCode = statusCode,
            Body = body,
            Headers = headers ?? new(StringComparer.OrdinalIgnoreCase),
            ElapsedMilliseconds = elapsedMilliseconds
        };

        return _validator.ValidateAgainstEndpointSchema(schema, fakeResponse);
    }
}

public class CallAndValidateResult
{
    public ApiResponse Response { get; set; } = new();
    public ValidationReport Validation { get; set; } = new();
}
