using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

var transport = new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:3001/mcp"),
        TransportMode = HttpTransportMode.StreamableHttp
    });

await using var client = await McpClient.CreateAsync(transport);

Console.WriteLine("==================================================");
Console.WriteLine(" Connected to API Testing MCP Server");
Console.WriteLine("==================================================");
Console.WriteLine();

var tools = (await client.ListToolsAsync()).ToList();

Console.WriteLine("Discovered MCP Tools:");
Console.WriteLine("---------------------");
foreach (var tool in tools)
{
    Console.WriteLine($" - {tool.Name}: {tool.Description}");
}
Console.WriteLine();

var healthCheckTool = tools.FirstOrDefault(t => string.Equals(t.Name, "check_api_health", StringComparison.OrdinalIgnoreCase));
var listEndpointsTool = tools.FirstOrDefault(t => string.Equals(t.Name, "list_endpoints", StringComparison.OrdinalIgnoreCase));
var generateScenariosTool = tools.FirstOrDefault(t => string.Equals(t.Name, "generate_test_scenarios", StringComparison.OrdinalIgnoreCase));
var runTestSuiteTool = tools.FirstOrDefault(t => string.Equals(t.Name, "run_test_suite", StringComparison.OrdinalIgnoreCase));

// ----------------------------------------------------
// Step 0: Pre-Flight Health Check
// ----------------------------------------------------
if (healthCheckTool != null)
{
    Console.WriteLine("==================================================");
    Console.WriteLine(" [Step 0] Pre-flight Check: Is Target API Online?");
    Console.WriteLine("==================================================");

    var healthResult = await healthCheckTool.InvokeAsync(new AIFunctionArguments());
    var healthJson = healthResult?.ToString() ?? "{}";
    using var healthDoc = JsonDocument.Parse(healthJson);
    var root = healthDoc.RootElement;

    bool isOnline = root.TryGetProperty("isOnline", out var onProp) && onProp.GetBoolean();
    string baseUrl = root.TryGetProperty("baseUrl", out var bProp) ? bProp.GetString() ?? "" : "";
    string message = root.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "" : "";

    if (!isOnline)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[ALERT] Target API is OFFLINE: {message}");
        Console.WriteLine($"Target URL : {baseUrl}");
        Console.WriteLine("Please start your target API service to execute live tests.");
        Console.ResetColor();
        Console.WriteLine();
        return;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[SUCCESS] Target API is ONLINE! ({baseUrl})");
    Console.ResetColor();
    Console.WriteLine();
}

// ----------------------------------------------------
// Step 1: Discover API Endpoints & Tags
// ----------------------------------------------------
string? selectedTag = null;
if (listEndpointsTool != null)
{
    Console.WriteLine("==================================================");
    Console.WriteLine(" [Step 1] Dynamically Discovering Endpoints...");
    Console.WriteLine("==================================================");

    var epResult = await listEndpointsTool.InvokeAsync(new AIFunctionArguments());
    var epJson = epResult?.ToString() ?? "[]";
    using var epDoc = JsonDocument.Parse(epJson);
    var endpoints = epDoc.RootElement.EnumerateArray().ToList();

    Console.WriteLine($"Total Endpoints Discovered: {endpoints.Count}");
    var allTags = endpoints
        .SelectMany(e => e.TryGetProperty("tags", out var t) ? t.EnumerateArray().Select(x => x.GetString()) : Enumerable.Empty<string?>())
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Distinct()
        .ToList();

    if (allTags.Any())
    {
        Console.WriteLine($"Discovered Tags: {string.Join(", ", allTags)}");
        // Prefer "Auth" if it exists, otherwise take first tag or test broadly
        selectedTag = allTags.FirstOrDefault(t => string.Equals(t, "Auth", StringComparison.OrdinalIgnoreCase)) ?? allTags.FirstOrDefault();
        Console.WriteLine($"Targeting Tag for Test Suite: {selectedTag}");
    }
    Console.WriteLine();
}

// ----------------------------------------------------
// Step 2: Automatic Test Scenario Generation
// ----------------------------------------------------
if (generateScenariosTool != null)
{
    Console.WriteLine("==================================================");
    Console.WriteLine(" [Step 2] Automatically Generating Test Scenarios...");
    Console.WriteLine("==================================================");

    var scenarioArgs = new Dictionary<string, object?>();
    if (!string.IsNullOrEmpty(selectedTag))
    {
        scenarioArgs["tagFilter"] = selectedTag;
    }

    var scenariosRaw = await generateScenariosTool.InvokeAsync(new AIFunctionArguments(scenarioArgs));
    var scenariosJson = scenariosRaw?.ToString() ?? "[]";
    using var scenariosDoc = JsonDocument.Parse(scenariosJson);
    var scenarios = scenariosDoc.RootElement.EnumerateArray().ToList();

    Console.WriteLine($"Generated {scenarios.Count} Test Scenarios (Happy Path, Missing Fields, Empty Values, Security, 404):");
    foreach (var s in scenarios.Take(6))
    {
        var id = s.GetProperty("id").GetString();
        var name = s.GetProperty("name").GetString();
        var category = s.GetProperty("category").GetString();
        Console.WriteLine($"  [{id}] ({category}) {name}");
    }
    if (scenarios.Count > 6)
    {
        Console.WriteLine($"  ... and {scenarios.Count - 6} more scenarios.");
    }
    Console.WriteLine();
}

// ----------------------------------------------------
// Step 3: Automated Test Suite Execution & Reporting
// ----------------------------------------------------
if (runTestSuiteTool != null)
{
    Console.WriteLine("==================================================");
    Console.WriteLine(" [Step 3] Executing Automated Test Suite...");
    Console.WriteLine("==================================================");

    var suiteArgs = new Dictionary<string, object?>();
    if (!string.IsNullOrEmpty(selectedTag))
    {
        suiteArgs["tagFilter"] = selectedTag;
    }

    var suiteResultRaw = await runTestSuiteTool.InvokeAsync(new AIFunctionArguments(suiteArgs));
    var suiteJson = suiteResultRaw?.ToString() ?? "{}";
    using var suiteDoc = JsonDocument.Parse(suiteJson);
    var suiteRoot = suiteDoc.RootElement;

    if (suiteRoot.TryGetProperty("summaryReport", out var summaryProp))
    {
        Console.WriteLine(summaryProp.GetString());
    }
    else
    {
        Console.WriteLine(suiteResultRaw);
    }
}

Console.WriteLine("==================================================");
Console.WriteLine(" Automated Testing & Reporting Completed!");
Console.WriteLine("==================================================");