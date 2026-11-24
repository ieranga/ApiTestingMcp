using System.ComponentModel;
using ApiTestingMcp.Server.Configuration;
using ApiTestingMcp.Server.Services;
using ApiTestingMcp.Server.Tools;
using ModelContextProtocol.Server;

// MCP clients (Claude Desktop, Claude Code, Cursor) launch this process themselves,
// usually from a different working directory, so resolve appsettings.json next to the binary.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Friendly environment variable overrides for MCP client configs:
//   TARGET_API_BASE_URL, TARGET_API_OPENAPI_PATH, TARGET_API_TIMEOUT_SECONDS
// (TARGET_API_TOKEN / TARGET_API_KEY / TARGET_API_KEY_HEADER are read by AuthManagerService.)
var envOverrides = new Dictionary<string, string?>();
void MapEnv(string envName, string configKey)
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(value)) envOverrides[configKey] = value.Trim();
}
MapEnv("TARGET_API_BASE_URL", $"{TargetApiOptions.SectionName}:BaseUrl");
MapEnv("TARGET_API_OPENAPI_PATH", $"{TargetApiOptions.SectionName}:OpenApiPath");
MapEnv("TARGET_API_TIMEOUT_SECONDS", $"{TargetApiOptions.SectionName}:TimeoutSeconds");
builder.Configuration.AddInMemoryCollection(envOverrides);

// stdout is reserved for MCP JSON-RPC messages, so all logs must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Configuration
builder.Services.Configure<TargetApiOptions>(builder.Configuration.GetSection(TargetApiOptions.SectionName));
builder.Services.AddSingleton<TargetApiContext>();

// Services
builder.Services.AddSingleton<AuthManagerService>();
builder.Services.AddSingleton<ResponseValidatorService>();
builder.Services.AddHttpClient<ApiClientService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Local development APIs often use self-signed dev certificates.
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
builder.Services.AddHttpClient<OpenApiService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
builder.Services.AddTransient<TestGeneratorService>();

builder.Services.AddTransient<ApiTestingTools>();
builder.Services.AddTransient<ApiDiscoveryTools>();
builder.Services.AddTransient<AuthTools>();
builder.Services.AddTransient<ApiValidationTools>();
builder.Services.AddTransient<TestSuiteTools>();

// MCP Server over stdio
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

// Simple connectivity check tool
[McpServerToolType]
public static class TestTool
{
    [McpServerTool]
    [Description("Returns a simple message to verify that the MCP server is working.")]
    public static string Hello()
    {
        return "Hello from API Testing MCP Server!";
    }
}
