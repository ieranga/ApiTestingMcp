using System.Text.Json;
using ApiTestingMcp.Server.Configuration;
using ApiTestingMcp.Server.Models;
using Microsoft.Extensions.Options;

namespace ApiTestingMcp.Server.Services;

public class OpenApiService
{
    private readonly HttpClient _httpClient;
    private readonly TargetApiOptions _options;
    private readonly ILogger<OpenApiService> _logger;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<EndpointDefinition>? _cachedEndpoints;
    private JsonDocument? _cachedRawDoc;
    private DateTimeOffset? _lastFetchedAt;

    private static readonly HashSet<string> HttpMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "patch", "delete", "head", "options", "trace"
    };

    public OpenApiService(
        HttpClient httpClient,
        IOptions<TargetApiOptions> options,
        ILogger<OpenApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("TargetApi:BaseUrl is not configured.");
        }

        if (Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }
        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task<IReadOnlyList<EndpointSummary>> GetEndpointsAsync(
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var definitions = await EnsureDefinitionsLoadedAsync(refresh, cancellationToken);
        return definitions.Select(d => new EndpointSummary
        {
            Method = d.Method,
            Path = d.Path,
            Summary = d.Summary,
            Description = d.Description,
            Tags = d.Tags
        }).ToList();
    }

    public async Task<EndpointDefinition?> GetEndpointSchemaAsync(
        string method,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var definitions = await EnsureDefinitionsLoadedAsync(refresh: false, cancellationToken);

        var normalizedMethod = method.Trim().ToUpperInvariant();
        var normalizedPath = endpoint.Trim();
        if (!normalizedPath.StartsWith("/"))
        {
            normalizedPath = "/" + normalizedPath;
        }

        return definitions.FirstOrDefault(d =>
            string.Equals(d.Method, normalizedMethod, StringComparison.OrdinalIgnoreCase) &&
            MatchesPath(d.Path, normalizedPath));
    }

    private static bool MatchesPath(string definedPath, string requestedPath)
    {
        // Exact match
        if (string.Equals(definedPath, requestedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Compare segment by segment considering {param} placeholders
        var definedSegments = definedPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var requestedSegments = requestedPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (definedSegments.Length != requestedSegments.Length)
        {
            return false;
        }

        for (int i = 0; i < definedSegments.Length; i++)
        {
            var defSeg = definedSegments[i];
            var reqSeg = requestedSegments[i];

            if (defSeg.StartsWith("{") && defSeg.EndsWith("}"))
            {
                continue; // Matches any parameter value
            }

            if (!string.Equals(defSeg, reqSeg, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<List<EndpointDefinition>> EnsureDefinitionsLoadedAsync(
        bool refresh,
        CancellationToken cancellationToken)
    {
        if (!refresh && _cachedEndpoints != null)
        {
            return _cachedEndpoints;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && _cachedEndpoints != null)
            {
                return _cachedEndpoints;
            }

            var doc = await DownloadOpenApiDocumentAsync(cancellationToken);
            _cachedRawDoc?.Dispose();
            _cachedRawDoc = doc;

            var parsed = ParseOpenApiDocument(doc);
            _cachedEndpoints = parsed;
            _lastFetchedAt = DateTimeOffset.UtcNow;
            return parsed;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<JsonDocument> DownloadOpenApiDocumentAsync(CancellationToken cancellationToken)
    {
        var candidatePaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.OpenApiPath))
        {
            candidatePaths.Add(_options.OpenApiPath);
        }
        candidatePaths.Add("/swagger/v1/swagger.json");
        candidatePaths.Add("/openapi/v1.json");
        candidatePaths.Add("/swagger.json");

        string? lastError = null;

        foreach (var path in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(_httpClient.BaseAddress!, path.TrimStart('/'));
                _logger.LogInformation("Attempting to fetch OpenAPI document from {Uri}", uri);

                var response = await _httpClient.GetAsync(uri, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    return JsonDocument.Parse(json);
                }

                lastError = $"Status {(int)response.StatusCode} ({response.ReasonPhrase}) from {uri}";
            }
            catch (Exception ex)
            {
                lastError = $"Error requesting {path}: {ex.Message}";
                _logger.LogDebug(ex, "Failed downloading OpenAPI from {Path}", path);
            }
        }

        throw new InvalidOperationException(
            $"Failed to retrieve OpenAPI document from target API at '{_options.BaseUrl}'. Last error: {lastError}");
    }

    private List<EndpointDefinition> ParseOpenApiDocument(JsonDocument doc)
    {
        var list = new List<EndpointDefinition>();
        var root = doc.RootElement;

        if (!root.TryGetProperty("paths", out var pathsElement) || pathsElement.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("OpenAPI document does not contain a valid 'paths' object.");
            return list;
        }

        foreach (var pathProperty in pathsElement.EnumerateObject())
        {
            var path = pathProperty.Name;
            var pathItem = pathProperty.Value;

            if (pathItem.ValueKind != JsonValueKind.Object) continue;

            // Extract common path-level parameters
            var commonParameters = new List<ParameterDefinition>();
            if (pathItem.TryGetProperty("parameters", out var pathParams) && pathParams.ValueKind == JsonValueKind.Array)
            {
                foreach (var paramElem in pathParams.EnumerateArray())
                {
                    var param = ParseParameter(paramElem, root);
                    if (param != null) commonParameters.Add(param);
                }
            }

            foreach (var opProperty in pathItem.EnumerateObject())
            {
                var method = opProperty.Name;
                if (!HttpMethodNames.Contains(method)) continue;

                var opItem = opProperty.Value;
                if (opItem.ValueKind != JsonValueKind.Object) continue;

                var definition = new EndpointDefinition
                {
                    Method = method.ToUpperInvariant(),
                    Path = path
                };

                if (opItem.TryGetProperty("summary", out var summaryProp))
                {
                    definition.Summary = summaryProp.GetString();
                }

                if (opItem.TryGetProperty("description", out var descProp))
                {
                    definition.Description = descProp.GetString();
                }

                if (opItem.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsProp.EnumerateArray())
                    {
                        var tagStr = tag.GetString();
                        if (!string.IsNullOrEmpty(tagStr)) definition.Tags.Add(tagStr);
                    }
                }

                // Add parameters (common + operation-specific)
                definition.Parameters.AddRange(commonParameters);
                if (opItem.TryGetProperty("parameters", out var opParams) && opParams.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in opParams.EnumerateArray())
                    {
                        var parsedParam = ParseParameter(p, root);
                        if (parsedParam != null)
                        {
                            // Avoid duplicate parameters by name & in
                            definition.Parameters.RemoveAll(existing =>
                                string.Equals(existing.Name, parsedParam.Name, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(existing.In, parsedParam.In, StringComparison.OrdinalIgnoreCase));
                            definition.Parameters.Add(parsedParam);
                        }
                    }
                }

                // Parse RequestBody (OpenAPI 3.x)
                if (opItem.TryGetProperty("requestBody", out var rbProp) && rbProp.ValueKind == JsonValueKind.Object)
                {
                    definition.RequestBody = ParseRequestBody(rbProp, root);
                }
                else
                {
                    // Check for Swagger 2.0 body parameter
                    var bodyParam = definition.Parameters.FirstOrDefault(p => string.Equals(p.In, "body", StringComparison.OrdinalIgnoreCase));
                    if (bodyParam != null)
                    {
                        definition.RequestBody = new RequestBodyDefinition
                        {
                            Description = bodyParam.Description,
                            Required = bodyParam.Required,
                            ContentType = "application/json",
                            Schema = bodyParam.Schema
                        };
                        definition.Parameters.Remove(bodyParam);
                    }
                }

                // Parse Responses
                if (opItem.TryGetProperty("responses", out var responsesProp) && responsesProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var respProp in responsesProp.EnumerateObject())
                    {
                        var statusCode = respProp.Name;
                        var respItem = respProp.Value;
                        definition.Responses[statusCode] = ParseResponse(statusCode, respItem, root);
                    }
                }

                list.Add(definition);
            }
        }

        return list;
    }

    private static ParameterDefinition? ParseParameter(JsonElement paramElement, JsonElement root)
    {
        if (paramElement.ValueKind != JsonValueKind.Object) return null;

        var resolved = ResolveRef(paramElement, root);
        if (resolved.ValueKind != JsonValueKind.Object) return null;

        var name = resolved.TryGetProperty("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrEmpty(name)) return null;

        var inLocation = resolved.TryGetProperty("in", out var inProp) ? inProp.GetString() : "query";
        var required = resolved.TryGetProperty("required", out var req) && req.GetBoolean();
        var description = resolved.TryGetProperty("description", out var d) ? d.GetString() : null;

        string? type = null;
        object? schemaObj = null;

        if (resolved.TryGetProperty("schema", out var schemaProp))
        {
            var resolvedSchema = ResolveRef(schemaProp, root);
            schemaObj = SimplifySchema(resolvedSchema, root);
            type = resolvedSchema.TryGetProperty("type", out var t) ? t.GetString() : null;
        }
        else if (resolved.TryGetProperty("type", out var typeProp))
        {
            type = typeProp.GetString();
        }

        return new ParameterDefinition
        {
            Name = name,
            In = inLocation ?? "query",
            Required = required,
            Description = description,
            Type = type,
            Schema = schemaObj
        };
    }

    private static RequestBodyDefinition ParseRequestBody(JsonElement rbElement, JsonElement root)
    {
        var resolved = ResolveRef(rbElement, root);
        var description = resolved.TryGetProperty("description", out var d) ? d.GetString() : null;
        var required = resolved.TryGetProperty("required", out var req) && req.GetBoolean();

        string contentType = "application/json";
        object? schema = null;

        if (resolved.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var mediaType in contentProp.EnumerateObject())
            {
                contentType = mediaType.Name;
                if (mediaType.Value.TryGetProperty("schema", out var s))
                {
                    var resolvedSchema = ResolveRef(s, root);
                    schema = SimplifySchema(resolvedSchema, root);
                }
                break; // Take primary content type (typically application/json)
            }
        }

        return new RequestBodyDefinition
        {
            Description = description,
            Required = required,
            ContentType = contentType,
            Schema = schema
        };
    }

    private static ResponseDefinition ParseResponse(string statusCode, JsonElement respElement, JsonElement root)
    {
        var resolved = ResolveRef(respElement, root);
        var description = resolved.TryGetProperty("description", out var d) ? d.GetString() : null;

        string? contentType = null;
        object? schema = null;

        if (resolved.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var mediaType in contentProp.EnumerateObject())
            {
                contentType = mediaType.Name;
                if (mediaType.Value.TryGetProperty("schema", out var s))
                {
                    var resolvedSchema = ResolveRef(s, root);
                    schema = SimplifySchema(resolvedSchema, root);
                }
                break;
            }
        }
        else if (resolved.TryGetProperty("schema", out var directSchema))
        {
            // Swagger 2.0 direct schema
            contentType = "application/json";
            var resolvedSchema = ResolveRef(directSchema, root);
            schema = SimplifySchema(resolvedSchema, root);
        }

        return new ResponseDefinition
        {
            StatusCode = statusCode,
            Description = description,
            ContentType = contentType,
            Schema = schema
        };
    }

    private static JsonElement ResolveRef(JsonElement element, JsonElement root)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string? refPath = null;
            if (element.TryGetProperty("$ref", out var refProp))
            {
                refPath = refProp.GetString();
            }
            else if (element.TryGetProperty("ref", out var altRefProp))
            {
                refPath = altRefProp.GetString();
            }

            if (!string.IsNullOrEmpty(refPath) && refPath.StartsWith("#/"))
            {
                var segments = refPath[2..].Split('/');
                var current = root;
                foreach (var seg in segments)
                {
                    if (current.ValueKind == JsonValueKind.Object)
                    {
                        bool found = false;
                        foreach (var prop in current.EnumerateObject())
                        {
                            if (string.Equals(prop.Name, seg, StringComparison.OrdinalIgnoreCase))
                            {
                                current = prop.Value;
                                found = true;
                                break;
                            }
                        }
                        if (!found) return element;
                    }
                    else
                    {
                        return element; // Couldn't resolve
                    }
                }
                return current;
            }
        }

        return element;
    }

    private static object? SimplifySchema(JsonElement schema, JsonElement root, int depth = 0)
    {
        if (depth > 5) return "object"; // Avoid cyclic recursion

        var resolved = ResolveRef(schema, root);
        if (resolved.ValueKind != JsonValueKind.Object) return null;

        var dict = new Dictionary<string, object?>();

        if (resolved.TryGetProperty("type", out var typeProp))
        {
            dict["type"] = typeProp.GetString();
        }
        if (resolved.TryGetProperty("format", out var formatProp))
        {
            dict["format"] = formatProp.GetString();
        }
        if (resolved.TryGetProperty("description", out var descProp))
        {
            dict["description"] = descProp.GetString();
        }

        if (resolved.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            var propDict = new Dictionary<string, object?>();
            foreach (var p in props.EnumerateObject())
            {
                propDict[p.Name] = SimplifySchema(p.Value, root, depth + 1);
            }
            dict["properties"] = propDict;
        }

        if (resolved.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.Array)
        {
            var reqList = new List<string>();
            foreach (var r in reqProp.EnumerateArray())
            {
                var s = r.GetString();
                if (s != null) reqList.Add(s);
            }
            dict["required"] = reqList;
        }

        if (resolved.TryGetProperty("items", out var itemsProp))
        {
            dict["items"] = SimplifySchema(itemsProp, root, depth + 1);
        }

        return dict;
    }
}
