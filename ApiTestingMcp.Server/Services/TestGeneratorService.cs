using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ApiTestingMcp.Server.Configuration;
using ApiTestingMcp.Server.Models;
using Microsoft.Extensions.Options;

namespace ApiTestingMcp.Server.Services;

public class TestGeneratorService
{
    private readonly OpenApiService _openApiService;
    private readonly ApiClientService _apiClient;
    private readonly ResponseValidatorService _validator;
    private readonly TargetApiContext _targetContext;
    private readonly ILogger<TestGeneratorService> _logger;

    public TestGeneratorService(
        OpenApiService openApiService,
        ApiClientService apiClient,
        ResponseValidatorService validator,
        TargetApiContext targetContext,
        ILogger<TestGeneratorService> logger)
    {
        _openApiService = openApiService;
        _apiClient = apiClient;
        _validator = validator;
        _targetContext = targetContext;
        _logger = logger;
    }

    public async Task<List<TestScenario>> GenerateScenariosAsync(
        string? endpoint = null,
        string? method = null,
        string? tagFilter = null,
        CancellationToken cancellationToken = default)
    {
        var scenarios = new List<TestScenario>();

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var singleMethod = method ?? "GET";
            var schema = await _openApiService.GetEndpointSchemaAsync(singleMethod, endpoint, cancellationToken);
            if (schema != null)
            {
                scenarios.AddRange(GenerateScenariosForEndpoint(schema));
            }
            return scenarios;
        }

        var allEndpoints = await _openApiService.GetEndpointsAsync(refresh: false, cancellationToken);
        int counter = 1;

        foreach (var epSummary in allEndpoints)
        {
            if (!string.IsNullOrWhiteSpace(tagFilter) &&
                !epSummary.Tags.Any(t => t.Contains(tagFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(method) &&
                !string.Equals(epSummary.Method, method, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fullSchema = await _openApiService.GetEndpointSchemaAsync(epSummary.Method, epSummary.Path, cancellationToken);
            if (fullSchema != null)
            {
                var generated = GenerateScenariosForEndpoint(fullSchema, ref counter);
                scenarios.AddRange(generated);
            }
        }

        return scenarios;
    }

    public List<TestScenario> GenerateScenariosForEndpoint(EndpointDefinition endpoint)
    {
        int counter = 1;
        return GenerateScenariosForEndpoint(endpoint, ref counter);
    }

    private List<TestScenario> GenerateScenariosForEndpoint(EndpointDefinition endpoint, ref int counter)
    {
        var list = new List<TestScenario>();
        var cleanPath = endpoint.Path;

        // 1. Happy Path Valid Request
        var happyPathScenario = CreateHappyPathScenario(endpoint, ref counter);
        list.Add(happyPathScenario);

        // 2. Security Test: Unauthorized Access (if endpoint documents 401 or modifies state)
        if (endpoint.Responses.ContainsKey("401") || endpoint.Method is "POST" or "PUT" or "DELETE" or "PATCH" || endpoint.Path.Contains("my", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new TestScenario
            {
                Id = $"TC-{counter++:D3}",
                Name = $"{endpoint.Method} {endpoint.Path} - Unauthorized Request",
                Category = "Security",
                Method = endpoint.Method,
                Endpoint = FillPathParameters(cleanPath, useValid: true),
                Description = "Calls endpoint without authentication to verify 401 Unauthorized enforcement.",
                UseAuth = false,
                RequestBody = happyPathScenario.RequestBody,
                ExpectedValidation = new ValidationRule
                {
                    ExpectedStatusCode = 401
                }
            });
        }

        // 3. Not Found Resource Test (if path contains parameters like {id})
        if (cleanPath.Contains('{'))
        {
            list.Add(new TestScenario
            {
                Id = $"TC-{counter++:D3}",
                Name = $"{endpoint.Method} {endpoint.Path} - Non-Existent Resource (404)",
                Category = "NotFound",
                Method = endpoint.Method,
                Endpoint = FillPathParameters(cleanPath, useValid: false),
                Description = "Queries non-existent resource identifier to verify 404 Not Found response.",
                UseAuth = true,
                RequestBody = happyPathScenario.RequestBody,
                ExpectedValidation = new ValidationRule
                {
                    ExpectedStatusCode = 404
                }
            });
        }

        // 4. Request Body Validation Scenarios (for POST, PUT, PATCH with request body)
        if (endpoint.RequestBody?.Schema != null)
        {
            var bodyScenarios = GenerateRequestBodyScenarios(endpoint, endpoint.RequestBody.Schema, ref counter);
            list.AddRange(bodyScenarios);
        }

        return list;
    }

    private static TestScenario CreateHappyPathScenario(EndpointDefinition endpoint, ref int counter)
    {
        int expectedStatus = 200;
        if (endpoint.Responses.ContainsKey("201")) expectedStatus = 201;
        else if (endpoint.Responses.ContainsKey("204")) expectedStatus = 204;
        else if (endpoint.Responses.ContainsKey("200")) expectedStatus = 200;

        string? requestBody = null;
        if (endpoint.RequestBody?.Schema != null)
        {
            requestBody = GenerateMockJsonFromSchema(endpoint.RequestBody.Schema, mockType: MockStrategy.Valid);
        }

        return new TestScenario
        {
            Id = $"TC-{counter++:D3}",
            Name = $"{endpoint.Method} {endpoint.Path} - Valid Request",
            Category = "HappyPath",
            Method = endpoint.Method,
            Endpoint = FillPathParameters(endpoint.Path, useValid: true),
            Description = endpoint.Summary ?? "Valid request conforming to schema.",
            UseAuth = true,
            RequestBody = requestBody,
            ExpectedValidation = new ValidationRule
            {
                ExpectedStatusCode = expectedStatus
            }
        };
    }

    private static List<TestScenario> GenerateRequestBodyScenarios(
        EndpointDefinition endpoint,
        object schemaObj,
        ref int counter)
    {
        var list = new List<TestScenario>();
        if (schemaObj is not Dictionary<string, object?> schemaDict) return list;

        // A. Missing Required Fields
        if (schemaDict.TryGetValue("required", out var reqObj) && reqObj is List<string> reqList)
        {
            foreach (var reqField in reqList.Take(3)) // Test up to 3 required fields
            {
                var omittedPayload = GenerateMockJsonFromSchema(schemaObj, MockStrategy.OmitField, omitField: reqField);
                list.Add(new TestScenario
                {
                    Id = $"TC-{counter++:D3}",
                    Name = $"{endpoint.Method} {endpoint.Path} - Missing Required Field '{reqField}'",
                    Category = "Validation",
                    Method = endpoint.Method,
                    Endpoint = FillPathParameters(endpoint.Path, useValid: true),
                    Description = $"Omits required property '{reqField}' to verify 400 Bad Request validation.",
                    UseAuth = true,
                    RequestBody = omittedPayload,
                    ExpectedValidation = new ValidationRule
                    {
                        ExpectedStatusCode = 400
                    }
                });
            }
        }

        // B. Empty String Values
        var emptyPayload = GenerateMockJsonFromSchema(schemaObj, MockStrategy.EmptyValues);
        if (!string.IsNullOrEmpty(emptyPayload) && emptyPayload != "{}")
        {
            list.Add(new TestScenario
            {
                Id = $"TC-{counter++:D3}",
                Name = $"{endpoint.Method} {endpoint.Path} - Empty String Values",
                Category = "Validation",
                Method = endpoint.Method,
                Endpoint = FillPathParameters(endpoint.Path, useValid: true),
                Description = "Sends empty strings for required fields to verify input validation.",
                UseAuth = true,
                RequestBody = emptyPayload,
                ExpectedValidation = new ValidationRule
                {
                    ExpectedStatusCode = 400
                }
            });
        }

        return list;
    }

    public async Task<TestSuiteResult> RunTestSuiteAsync(
        List<TestScenario>? scenarios = null,
        string? endpoint = null,
        string? tagFilter = null,
        CancellationToken cancellationToken = default)
    {
        var testList = scenarios ?? await GenerateScenariosAsync(endpoint, tagFilter: tagFilter, cancellationToken: cancellationToken);

        var suiteResult = new TestSuiteResult
        {
            SuiteName = string.IsNullOrEmpty(endpoint) ? "Automated API Test Suite" : $"Test Suite for {endpoint}",
            TargetApiBaseUrl = _targetContext.BaseUrl,
            ExecutedAt = DateTimeOffset.UtcNow,
            TotalTests = testList.Count
        };

        var stopwatch = Stopwatch.StartNew();

        foreach (var scenario in testList)
        {
            var testExec = await ExecuteScenarioAsync(scenario, cancellationToken);
            suiteResult.TestResults.Add(testExec);

            if (testExec.IsPassed)
            {
                suiteResult.PassedCount++;
            }
            else
            {
                suiteResult.FailedCount++;
            }
        }

        stopwatch.Stop();
        suiteResult.TotalDurationMs = stopwatch.ElapsedMilliseconds;

        // Build human/AI friendly markdown summary
        suiteResult.SummaryReport = BuildMarkdownReport(suiteResult);

        return suiteResult;
    }

    private async Task<TestCaseExecutionResult> ExecuteScenarioAsync(
        TestScenario scenario,
        CancellationToken cancellationToken)
    {
        var request = new ApiRequest
        {
            Method = scenario.Method,
            Endpoint = scenario.Endpoint,
            Body = scenario.RequestBody,
            Headers = scenario.RequestHeaders,
            QueryParameters = scenario.QueryParameters,
            UseAuth = scenario.UseAuth
        };

        var response = await _apiClient.SendAsync(request, cancellationToken);

        var validationReport = _validator.Validate(
            response.StatusCode,
            response.Body,
            response.Headers,
            response.ElapsedMilliseconds,
            scenario.ExpectedValidation);

        var result = new TestCaseExecutionResult
        {
            ScenarioId = scenario.Id,
            TestName = scenario.Name,
            Category = scenario.Category,
            Method = scenario.Method,
            Endpoint = scenario.Endpoint,
            ActualStatusCode = response.StatusCode,
            ExpectedStatusCode = scenario.ExpectedValidation.ExpectedStatusCode,
            IsPassed = validationReport.IsPassed,
            ElapsedMs = response.ElapsedMilliseconds,
            ResponseBodyPreview = FormatBodyPreview(response.Body),
            Validation = validationReport
        };

        if (!result.IsPassed)
        {
            result.LikelyCauseOfFailure = DetermineLikelyCause(scenario, response, validationReport);
        }

        return result;
    }

    private static string DetermineLikelyCause(TestScenario scenario, ApiResponse response, ValidationReport validation)
    {
        var expected = scenario.ExpectedValidation.ExpectedStatusCode;
        var actual = response.StatusCode;

        if (actual == 0)
        {
            return "Connection Failure: Target API server was unreachable or refused connection.";
        }
        if (actual == 500)
        {
            return "Server Error (500): Server threw an unhandled exception instead of properly validating input.";
        }
        if (actual == 401 && expected != 401)
        {
            return "Unauthorized (401): Endpoint requires valid authentication token or credentials.";
        }
        if (actual == 403)
        {
            return "Forbidden (403): Current credentials lack permissions for this resource.";
        }
        if (actual == 404 && expected != 404)
        {
            return "Resource Not Found (404): Endpoint path or test ID does not exist on target server.";
        }
        if ((actual == 200 || actual == 201) && expected == 400)
        {
            return "Missing Input Validation: Server accepted invalid/missing input instead of returning 400 Bad Request.";
        }

        return validation.Summary;
    }

    private static string BuildMarkdownReport(TestSuiteResult suite)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# API Test Suite Report");
        sb.AppendLine($"**Target API:** `{suite.TargetApiBaseUrl}`  ");
        sb.AppendLine($"**Overall Status:** **{suite.OverallStatus}** ({suite.PassedCount}/{suite.TotalTests} Passed)  ");
        sb.AppendLine($"**Duration:** {suite.TotalDurationMs}ms | **Executed:** {suite.ExecutedAt:yyyy-MM-dd HH:mm:ss UTC}\n");

        sb.AppendLine("| ID | Test Scenario | Category | Method | Status | Expected | Actual | Result |");
        sb.AppendLine("|:---|:---|:---|:---|:---|:---|:---|:---|");

        foreach (var t in suite.TestResults)
        {
            var badge = t.IsPassed ? "PASS" : "**FAIL**";
            sb.AppendLine($"| `{t.ScenarioId}` | {t.TestName} | {t.Category} | `{t.Method}` | `{t.Endpoint}` | `{t.ExpectedStatusCode}` | `{t.ActualStatusCode}` | {badge} |");
        }

        var failures = suite.TestResults.Where(t => !t.IsPassed).ToList();
        if (failures.Count > 0)
        {
            sb.AppendLine("\n## Failure Analysis");
            foreach (var f in failures)
            {
                sb.AppendLine($"### {f.ScenarioId}: {f.TestName}");
                sb.AppendLine($"- **Endpoint:** `{f.Method} {f.Endpoint}`");
                sb.AppendLine($"- **Expected Status:** `{f.ExpectedStatusCode}` | **Actual Status:** `{f.ActualStatusCode}`");
                sb.AppendLine($"- **Likely Cause:** {f.LikelyCauseOfFailure}");
                if (!string.IsNullOrEmpty(f.ResponseBodyPreview))
                {
                    sb.AppendLine($"- **Response:** `{f.ResponseBodyPreview}`");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FillPathParameters(string path, bool useValid)
    {
        if (!path.Contains('{')) return path;

        // Replace common {id} or {categoryId} or {serviceId} parameters
        var parts = path.Split('/');
        var sb = new StringBuilder();

        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.StartsWith('{') && p.EndsWith('}'))
            {
                var paramName = p[1..^1].ToLowerInvariant();
                string sampleVal;

                if (useValid)
                {
                    sampleVal = paramName.Contains("id") ? "1" : "sample";
                }
                else
                {
                    sampleVal = "999999";
                }

                sb.Append(sampleVal);
            }
            else
            {
                sb.Append(p);
            }

            if (i < parts.Length - 1 && parts.Length > 1) sb.Append('/');
        }

        var res = sb.ToString();
        if (!res.StartsWith("/")) res = "/" + res;
        return res;
    }

    private static string? FormatBodyPreview(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var clean = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length > 120 ? clean[..120] + "..." : clean;
    }

    private enum MockStrategy { Valid, OmitField, EmptyValues }

    private static string GenerateMockJsonFromSchema(
        object schemaObj,
        MockStrategy mockType = MockStrategy.Valid,
        string? omitField = null)
    {
        if (schemaObj is not Dictionary<string, object?> dict) return "{}";

        var propertiesObj = dict.TryGetValue("properties", out var p) ? p as Dictionary<string, object?> : null;
        if (propertiesObj == null) return "{}";

        var result = new Dictionary<string, object?>();

        foreach (var (propName, propVal) in propertiesObj)
        {
            if (mockType == MockStrategy.OmitField && string.Equals(propName, omitField, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Skip this field
            }

            if (propVal is Dictionary<string, object?> propDict)
            {
                var type = propDict.TryGetValue("type", out var t) ? t?.ToString()?.ToLowerInvariant() : "string";
                var format = propDict.TryGetValue("format", out var f) ? f?.ToString()?.ToLowerInvariant() : null;

                if (mockType == MockStrategy.EmptyValues && type == "string")
                {
                    result[propName] = "";
                    continue;
                }

                result[propName] = type switch
                {
                    "string" when format == "email" => "test@example.com",
                    "string" when format == "uuid" => Guid.NewGuid().ToString(),
                    "string" when format == "date-time" => DateTime.UtcNow.ToString("o"),
                    "string" => $"sample_{propName}",
                    "integer" or "int" => 10,
                    "number" or "double" or "float" => 99.99,
                    "boolean" or "bool" => true,
                    "array" => new object[] { },
                    "object" => new Dictionary<string, object>(),
                    _ => "sample_value"
                };
            }
        }

        return JsonSerializer.Serialize(result);
    }
}
