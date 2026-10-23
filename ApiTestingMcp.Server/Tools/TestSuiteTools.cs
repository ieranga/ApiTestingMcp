using System.ComponentModel;
using ApiTestingMcp.Server.Models;
using ApiTestingMcp.Server.Services;
using ModelContextProtocol.Server;

namespace ApiTestingMcp.Server.Tools;

[McpServerToolType]
public class TestSuiteTools
{
    private readonly TestGeneratorService _testGenerator;

    public TestSuiteTools(TestGeneratorService testGenerator)
    {
        _testGenerator = testGenerator;
    }

    [McpServerTool(Name = "generate_test_scenarios")]
    [Description("Automatically generates positive and negative test scenarios (happy path, missing fields, empty values, 404s, unauthorized) from the target API OpenAPI schema.")]
    public async Task<List<TestScenario>> GenerateTestScenarios(
        [Description("Optional specific endpoint to generate tests for (e.g. '/api/Auth/login'). If omitted, generates for all endpoints.")] string? endpoint = null,
        [Description("Optional HTTP method filter (e.g. 'GET', 'POST')")] string? method = null,
        [Description("Optional tag filter to target a specific controller/module (e.g. 'Auth', 'Marketplace', 'Listing')")] string? tagFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _testGenerator.GenerateScenariosAsync(endpoint, method, tagFilter, cancellationToken);
    }

    [McpServerTool(Name = "run_test_suite")]
    [Description("Generates and executes automated test scenarios against the target API, returning a comprehensive test report with PASS/FAIL results, timing, and failure analysis.")]
    public async Task<TestSuiteResult> RunTestSuite(
        [Description("Optional specific endpoint to test (e.g. '/api/Auth/users'). If omitted, tests all or filtered endpoints.")] string? endpoint = null,
        [Description("Optional tag filter to limit test scope (e.g. 'Auth', 'Listing', 'Marketplace')")] string? tagFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _testGenerator.RunTestSuiteAsync(
            scenarios: null,
            endpoint: endpoint,
            tagFilter: tagFilter,
            cancellationToken: cancellationToken);
    }
}
