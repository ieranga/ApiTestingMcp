namespace ApiTestingMcp.Server.Models;

public class TestScenario
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // HappyPath, Validation, Security, NotFound, Boundary
    public string Method { get; set; } = "GET";
    public string Endpoint { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RequestBody { get; set; }
    public Dictionary<string, string>? RequestHeaders { get; set; }
    public Dictionary<string, string>? QueryParameters { get; set; }
    public bool UseAuth { get; set; } = true;
    public ValidationRule ExpectedValidation { get; set; } = new();
}

public class TestCaseExecutionResult
{
    public string ScenarioId { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int ActualStatusCode { get; set; }
    public int? ExpectedStatusCode { get; set; }
    public bool IsPassed { get; set; }
    public long ElapsedMs { get; set; }
    public string? ResponseBodyPreview { get; set; }
    public ValidationReport Validation { get; set; } = new();
    public string? LikelyCauseOfFailure { get; set; }
}

public class TestSuiteResult
{
    public string SuiteName { get; set; } = "API Automated Test Suite";
    public string TargetApiBaseUrl { get; set; } = string.Empty;
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
    public long TotalDurationMs { get; set; }
    public int TotalTests { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public string OverallStatus => FailedCount == 0 && TotalTests > 0 ? "PASS" : "FAIL";
    public string SummaryReport { get; set; } = string.Empty;
    public List<TestCaseExecutionResult> TestResults { get; set; } = [];
}
