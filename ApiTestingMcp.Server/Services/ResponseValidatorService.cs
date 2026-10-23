using System.Text.Json;
using ApiTestingMcp.Server.Models;

namespace ApiTestingMcp.Server.Services;

public class ResponseValidatorService
{
    private readonly ILogger<ResponseValidatorService> _logger;

    public ResponseValidatorService(ILogger<ResponseValidatorService> logger)
    {
        _logger = logger;
    }

    public ValidationReport Validate(
        int statusCode,
        string? responseBody,
        Dictionary<string, string>? headers,
        long elapsedMilliseconds,
        ValidationRule rules)
    {
        var report = new ValidationReport();
        var assertions = new List<AssertionResult>();

        // 1. Status Code Validation
        if (rules.ExpectedStatusCode.HasValue)
        {
            var expected = rules.ExpectedStatusCode.Value;
            var passed = statusCode == expected;
            assertions.Add(new AssertionResult
            {
                Name = "HTTP Status Code",
                Passed = passed,
                Expected = expected.ToString(),
                Actual = statusCode.ToString(),
                ErrorMessage = passed ? null : $"Expected HTTP {expected}, but received HTTP {statusCode}."
            });
        }

        // 2. Latency SLA Validation
        if (rules.MaxLatencyMs.HasValue && rules.MaxLatencyMs.Value > 0)
        {
            var maxAllowed = rules.MaxLatencyMs.Value;
            var passed = elapsedMilliseconds <= maxAllowed;
            assertions.Add(new AssertionResult
            {
                Name = "Response Latency SLA",
                Passed = passed,
                Expected = $"<= {maxAllowed}ms",
                Actual = $"{elapsedMilliseconds}ms",
                ErrorMessage = passed ? null : $"Latency of {elapsedMilliseconds}ms exceeded maximum allowed SLA of {maxAllowed}ms."
            });
        }

        // 3. Expected Headers Validation
        if (rules.ExpectedHeaders != null && rules.ExpectedHeaders.Count > 0)
        {
            var responseHeaders = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (headerKey, expectedVal) in rules.ExpectedHeaders)
            {
                var found = responseHeaders.TryGetValue(headerKey, out var actualVal);
                var passed = found && (string.IsNullOrEmpty(expectedVal) || (actualVal != null && actualVal.Contains(expectedVal, StringComparison.OrdinalIgnoreCase)));

                assertions.Add(new AssertionResult
                {
                    Name = $"Header '{headerKey}'",
                    Passed = passed,
                    Expected = string.IsNullOrEmpty(expectedVal) ? "Header present" : expectedVal,
                    Actual = found ? (actualVal ?? "") : "Missing",
                    ErrorMessage = passed ? null : found
                        ? $"Header '{headerKey}' has value '{actualVal}', which does not contain '{expectedVal}'."
                        : $"Required header '{headerKey}' was missing from the response."
                });
            }
        }

        // 4. JSON Body Validations
        JsonDocument? doc = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                doc = JsonDocument.Parse(responseBody);
            }
        }
        catch (JsonException ex)
        {
            // Only report an error if JSON-specific rules were specified
            if ((rules.RequiredFields != null && rules.RequiredFields.Count > 0) ||
                (rules.FieldTypes != null && rules.FieldTypes.Count > 0) ||
                (rules.ValueAssertions != null && rules.ValueAssertions.Count > 0))
            {
                assertions.Add(new AssertionResult
                {
                    Name = "JSON Parsing",
                    Passed = false,
                    Expected = "Valid JSON body",
                    Actual = "Invalid JSON or plain text",
                    ErrorMessage = $"Failed to parse response body as JSON: {ex.Message}"
                });
            }
        }

        if (doc != null)
        {
            using (doc)
            {
                var root = doc.RootElement;

                // 4A. Required Fields
                if (rules.RequiredFields != null)
                {
                    foreach (var fieldPath in rules.RequiredFields)
                    {
                        if (string.IsNullOrWhiteSpace(fieldPath)) continue;

                        var found = TryGetElementByPath(root, fieldPath, out var element);
                        var passed = found && element.ValueKind != JsonValueKind.Null && element.ValueKind != JsonValueKind.Undefined;

                        assertions.Add(new AssertionResult
                        {
                            Name = $"Required Field '{fieldPath}'",
                            Passed = passed,
                            Expected = "Field present and not null",
                            Actual = found ? $"{element.ValueKind}" : "Missing",
                            ErrorMessage = passed ? null : found
                                ? $"Field '{fieldPath}' exists but has value 'null'."
                                : $"Required field '{fieldPath}' was not found in the response JSON."
                        });
                    }
                }

                // 4B. Field Types
                if (rules.FieldTypes != null)
                {
                    foreach (var (fieldPath, expectedType) in rules.FieldTypes)
                    {
                        var found = TryGetElementByPath(root, fieldPath, out var element);
                        if (!found)
                        {
                            assertions.Add(new AssertionResult
                            {
                                Name = $"Field Type '{fieldPath}'",
                                Passed = false,
                                Expected = expectedType,
                                Actual = "Missing",
                                ErrorMessage = $"Field '{fieldPath}' not found, cannot verify type."
                            });
                            continue;
                        }

                        var actualTypeStr = GetNormalizedTypeName(element);
                        var passed = MatchesType(element, expectedType);

                        assertions.Add(new AssertionResult
                        {
                            Name = $"Field Type '{fieldPath}'",
                            Passed = passed,
                            Expected = expectedType,
                            Actual = actualTypeStr,
                            ErrorMessage = passed ? null : $"Field '{fieldPath}' is of type '{actualTypeStr}', but expected '{expectedType}'."
                        });
                    }
                }

                // 4C. Value Assertions
                if (rules.ValueAssertions != null)
                {
                    foreach (var (fieldPath, expectedValue) in rules.ValueAssertions)
                    {
                        var found = TryGetElementByPath(root, fieldPath, out var element);
                        if (!found)
                        {
                            assertions.Add(new AssertionResult
                            {
                                Name = $"Assertion '{fieldPath}'",
                                Passed = false,
                                Expected = expectedValue,
                                Actual = "Missing",
                                ErrorMessage = $"Field '{fieldPath}' not found, cannot assert value."
                            });
                            continue;
                        }

                        var actualStr = element.ToString() ?? "";
                        var passed = EvaluateValueAssertion(element, expectedValue);

                        assertions.Add(new AssertionResult
                        {
                            Name = $"Assertion '{fieldPath}'",
                            Passed = passed,
                            Expected = expectedValue,
                            Actual = actualStr,
                            ErrorMessage = passed ? null : $"Field '{fieldPath}' value '{actualStr}' did not satisfy assertion '{expectedValue}'."
                        });
                    }
                }
            }
        }
        else if (rules.RequiredFields != null && rules.RequiredFields.Count > 0 && string.IsNullOrWhiteSpace(responseBody))
        {
            assertions.Add(new AssertionResult
            {
                Name = "Response Body Content",
                Passed = false,
                Expected = "Non-empty JSON body",
                Actual = "Empty body",
                ErrorMessage = "Response body was empty, but required JSON fields were specified."
            });
        }

        // Summary compilation
        report.TotalAssertions = assertions.Count;
        report.PassedCount = assertions.Count(a => a.Passed);
        report.FailedCount = assertions.Count(a => !a.Passed);
        report.IsPassed = report.FailedCount == 0;
        report.Assertions = assertions;

        report.Summary = report.IsPassed
            ? $"PASS: All {report.TotalAssertions} assertion(s) satisfied."
            : $"FAIL: {report.FailedCount} of {report.TotalAssertions} assertion(s) failed.";

        return report;
    }

    public ValidationReport ValidateAgainstEndpointSchema(EndpointDefinition endpointSchema, ApiResponse response)
    {
        var rules = new ValidationRule
        {
            RequiredFields = [],
            FieldTypes = new(StringComparer.OrdinalIgnoreCase)
        };

        var statusCodeStr = response.StatusCode.ToString();

        // 1. Check if the status code is one of the documented response codes
        if (endpointSchema.Responses.Count > 0)
        {
            var documentedCodes = endpointSchema.Responses.Keys.ToList();
            if (documentedCodes.Contains(statusCodeStr))
            {
                rules.ExpectedStatusCode = response.StatusCode;
            }
            else if (documentedCodes.Any(c => int.TryParse(c, out var code) && code >= 200 && code < 300))
            {
                // Fallback to first documented 2xx code
                var first2xx = documentedCodes.FirstOrDefault(c => int.TryParse(c, out var code) && code >= 200 && code < 300);
                if (first2xx != null && int.TryParse(first2xx, out var codeInt))
                {
                    rules.ExpectedStatusCode = codeInt;
                }
            }
        }

        // 2. Extract schema properties if available for this status code
        if (endpointSchema.Responses.TryGetValue(statusCodeStr, out var responseDef) && responseDef.Schema != null)
        {
            ExtractRulesFromSchemaObject(responseDef.Schema, rules, prefix: "");
        }

        return Validate(response.StatusCode, response.Body, response.Headers, response.ElapsedMilliseconds, rules);
    }

    private static void ExtractRulesFromSchemaObject(object schemaObj, ValidationRule rules, string prefix)
    {
        if (schemaObj is not Dictionary<string, object?> dict) return;

        // Required list
        if (dict.TryGetValue("required", out var reqObj) && reqObj is List<string> reqList)
        {
            foreach (var r in reqList)
            {
                var fullPath = string.IsNullOrEmpty(prefix) ? r : $"{prefix}.{r}";
                rules.RequiredFields ??= [];
                rules.RequiredFields.Add(fullPath);
            }
        }

        // Properties
        if (dict.TryGetValue("properties", out var propObj) && propObj is Dictionary<string, object?> props)
        {
            foreach (var (propName, propVal) in props)
            {
                var fullPath = string.IsNullOrEmpty(prefix) ? propName : $"{prefix}.{propName}";
                if (propVal is Dictionary<string, object?> propDict && propDict.TryGetValue("type", out var typeVal) && typeVal is string typeStr)
                {
                    rules.FieldTypes ??= new(StringComparer.OrdinalIgnoreCase);
                    rules.FieldTypes[fullPath] = typeStr;

                    // If nested object, recurse
                    if (typeStr == "object" && propDict.ContainsKey("properties"))
                    {
                        ExtractRulesFromSchemaObject(propDict, rules, fullPath);
                    }
                }
            }
        }
    }

    private static bool TryGetElementByPath(JsonElement root, string path, out JsonElement element)
    {
        element = root;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

        foreach (var seg in segments)
        {
            var cleanSeg = seg.Trim();

            // Check if segment is an array index e.g. "items[0]" or "[0]"
            if (cleanSeg.StartsWith("[") && cleanSeg.EndsWith("]") &&
                int.TryParse(cleanSeg[1..^1], out var directIdx))
            {
                if (element.ValueKind != JsonValueKind.Array || directIdx < 0 || directIdx >= element.GetArrayLength())
                {
                    return false;
                }
                element = element[directIdx];
                continue;
            }

            if (cleanSeg.Contains('[') && cleanSeg.EndsWith("]"))
            {
                var bracketIdx = cleanSeg.IndexOf('[');
                var propName = cleanSeg[..bracketIdx];
                var indexStr = cleanSeg[(bracketIdx + 1)..^1];

                if (!element.TryGetProperty(propName, out var arrayElem) ||
                    arrayElem.ValueKind != JsonValueKind.Array ||
                    !int.TryParse(indexStr, out var idx) ||
                    idx < 0 || idx >= arrayElem.GetArrayLength())
                {
                    return false;
                }
                element = arrayElem[idx];
                continue;
            }

            // Normal property
            if (element.ValueKind == JsonValueKind.Object)
            {
                bool found = false;
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, cleanSeg, StringComparison.OrdinalIgnoreCase))
                    {
                        element = prop.Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            else if (element.ValueKind == JsonValueKind.Array && int.TryParse(cleanSeg, out var arrayNumIdx))
            {
                if (arrayNumIdx < 0 || arrayNumIdx >= element.GetArrayLength()) return false;
                element = element[arrayNumIdx];
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static string GetNormalizedTypeName(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => element.TryGetInt64(out _) ? "integer" : "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Array => $"array (count: {element.GetArrayLength()})",
            JsonValueKind.Object => "object",
            JsonValueKind.Null => "null",
            _ => element.ValueKind.ToString().ToLowerInvariant()
        };
    }

    private static bool MatchesType(JsonElement element, string expectedType)
    {
        var norm = expectedType.Trim().ToLowerInvariant();
        return norm switch
        {
            "string" => element.ValueKind == JsonValueKind.String,
            "int" or "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
            "number" or "double" or "float" or "decimal" => element.ValueKind == JsonValueKind.Number,
            "bool" or "boolean" => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" or "list" => element.ValueKind == JsonValueKind.Array,
            "object" => element.ValueKind == JsonValueKind.Object,
            "null" => element.ValueKind == JsonValueKind.Null,
            "uuid" or "guid" => element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out _),
            _ => true
        };
    }

    private static bool EvaluateValueAssertion(JsonElement element, string expectedValue)
    {
        var cleanExpected = expectedValue.Trim();

        // 1. "contains:..." assertion
        if (cleanExpected.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
        {
            var target = cleanExpected[9..].Trim();
            var actual = element.ToString() ?? "";
            return actual.Contains(target, StringComparison.OrdinalIgnoreCase);
        }

        // 2. "not_empty" or "non_empty" assertion
        if (cleanExpected.Equals("not_empty", StringComparison.OrdinalIgnoreCase) ||
            cleanExpected.Equals("non_empty", StringComparison.OrdinalIgnoreCase))
        {
            if (element.ValueKind == JsonValueKind.String) return !string.IsNullOrWhiteSpace(element.GetString());
            if (element.ValueKind == JsonValueKind.Array) return element.GetArrayLength() > 0;
            return element.ValueKind != JsonValueKind.Null;
        }

        // 3. Array count assertion e.g. "count > 0" or "> 0"
        if (element.ValueKind == JsonValueKind.Array)
        {
            var count = element.GetArrayLength();
            if (cleanExpected.StartsWith(">") && int.TryParse(cleanExpected[1..].Trim(), out var gt))
                return count > gt;
            if (cleanExpected.StartsWith(">=") && int.TryParse(cleanExpected[2..].Trim(), out var gte))
                return count >= gte;
            if (cleanExpected.StartsWith("<") && int.TryParse(cleanExpected[1..].Trim(), out var lt))
                return count < lt;
            if (int.TryParse(cleanExpected, out var exactCount))
                return count == exactCount;
        }

        // 4. Boolean comparison
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            bool.TryParse(cleanExpected, out var expectedBool))
        {
            return element.GetBoolean() == expectedBool;
        }

        // 5. Numeric comparison
        if (element.ValueKind == JsonValueKind.Number && double.TryParse(cleanExpected, out var expectedDouble))
        {
            return Math.Abs(element.GetDouble() - expectedDouble) < 0.0001;
        }

        // 6. Default string equality
        var actualStr = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();

        return string.Equals(actualStr, cleanExpected, StringComparison.OrdinalIgnoreCase);
    }
}
