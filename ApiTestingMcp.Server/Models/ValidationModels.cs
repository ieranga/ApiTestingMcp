namespace ApiTestingMcp.Server.Models;

public class ValidationRule
{
    public int? ExpectedStatusCode { get; set; }
    public List<string>? RequiredFields { get; set; }
    public Dictionary<string, string>? FieldTypes { get; set; }
    public Dictionary<string, string>? ValueAssertions { get; set; }
    public Dictionary<string, string>? ExpectedHeaders { get; set; }
    public long? MaxLatencyMs { get; set; }
}

public class AssertionResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class ValidationReport
{
    public bool IsPassed { get; set; }
    public int TotalAssertions { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<AssertionResult> Assertions { get; set; } = [];
}
