using System.ComponentModel;
using System.Text.Json;
using ApiTestingMcp.Server.Configuration;
using ApiTestingMcp.Server.Services;
using ApiTestingMcp.Server.Tools;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<TargetApiOptions>(builder.Configuration.GetSection(TargetApiOptions.SectionName));
builder.Services.AddSingleton<TargetApiContext>();

// Services
builder.Services.AddSingleton<AuthManagerService>();
builder.Services.AddSingleton<ResponseValidatorService>();
builder.Services.AddHttpClient<ApiClientService>();
builder.Services.AddHttpClient<OpenApiService>();
builder.Services.AddTransient<TestGeneratorService>();

builder.Services.AddTransient<ApiTestingTools>();
builder.Services.AddTransient<ApiDiscoveryTools>();
builder.Services.AddTransient<AuthTools>();
builder.Services.AddTransient<ApiValidationTools>();
builder.Services.AddTransient<TestSuiteTools>();

// MCP Server
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

// Root Endpoint: Interactive Web Terminal for browsers, JSON for API clients
app.MapGet("/", (HttpContext ctx, TargetApiContext targetContext) =>
{
    var accept = ctx.Request.Headers.Accept.ToString();
    var format = ctx.Request.Query["format"].ToString();

    if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) ||
        (accept.Contains("application/json") && !accept.Contains("text/html")))
    {
        return Results.Ok(new
        {
            status = "Online",
            service = "ApiTestingMcp Server (.NET 9)",
            targetBaseUrl = targetContext.BaseUrl,
            openApiPath = targetContext.OpenApiPath,
            mcpEndpoint = "/mcp",
            swaggerEndpoint = "/swagger/v1/swagger.json",
            description = "Model Context Protocol (MCP) API Testing Platform. Connect your AI assistant to /mcp."
        });
    }

    var html = WebTerminalPage.Render(targetContext.BaseUrl, targetContext.OpenApiPath);
    return Results.Content(html, "text/html; charset=utf-8");
});

// Interactive Web Terminal API Endpoints
app.MapPost("/api/web/set-target", (TargetApiContext targetContext, WebSetTargetRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.BaseUrl))
    {
        return Results.BadRequest(new { error = "Base URL is required." });
    }
    try
    {
        targetContext.UpdateTarget(req.BaseUrl, req.OpenApiPath);
        return Results.Ok(new
        {
            success = true,
            targetBaseUrl = targetContext.BaseUrl,
            openApiPath = targetContext.OpenApiPath
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/web/health", async (ApiClientService client, CancellationToken ct) =>
{
    var status = await client.CheckHealthAsync(null, ct);
    return Results.Ok(status);
});

app.MapPost("/api/web/discover", async (OpenApiService openApi, CancellationToken ct) =>
{
    var endpoints = await openApi.GetEndpointsAsync(refresh: true, ct);
    return Results.Ok(endpoints);
});

app.MapPost("/api/web/run-tests", async (TestGeneratorService testGen, CancellationToken ct) =>
{
    try
    {
        var result = await testGen.RunTestSuiteAsync(null, null, null, ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = ex.Message });
    }
});

// MCP Endpoint
app.MapMcp("/mcp");

// --- Sample Target Web API Endpoints for Local Testing ---
var users = new Dictionary<int, UserDto>
{
    [10] = new(10, "Alice Johnson", "alice@example.com")
};

app.MapGet("/api/users", () => Results.Ok(users.Values));

app.MapGet("/api/users/{id:int}", (int id) =>
{
    return users.TryGetValue(id, out var user)
        ? Results.Ok(user)
        : Results.NotFound(new { error = $"User with ID {id} was not found." });
});

app.MapPost("/api/users", (UserCreateModel model) =>
{
    if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Email))
    {
        return Results.BadRequest(new { error = "Name and Email are required fields." });
    }

    var newId = users.Keys.DefaultIfEmpty(0).Max() + 1;
    var createdUser = new UserDto(newId, model.Name.Trim(), model.Email.Trim());
    users[newId] = createdUser;

    return Results.Created($"/api/users/{newId}", createdUser);
});

app.MapDelete("/api/users/{id:int}", (int id) =>
{
    return users.Remove(id)
        ? Results.NoContent()
        : Results.NotFound(new { error = $"User with ID {id} was not found." });
});

app.MapPost("/api/Auth/login", (LoginRequestDto login) =>
{
    if (login.Email == "user1@gmail.com" && login.Password == "password123")
    {
        var sampleToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyMS1pZCIsImVtYWlsIjoidXNlcjFAZ21haWwuY29tIiwicm9sZSI6IlVzZXIiLCJleHAiOjE4OTM0NTYwMDB9.sampleSignature";
        return Results.Ok(new { success = true, token = sampleToken, message = "Login successful" });
    }
    return Results.Unauthorized();
});

app.MapGet("/api/secure/profile", (HttpContext ctx) =>
{
    var authHeader = ctx.Request.Headers.Authorization.ToString();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Unauthorized();
    }
    return Results.Ok(new { success = true, message = "Access granted to secure profile!", user = "user1@gmail.com" });
});

// Sample OpenAPI / Swagger Specification Endpoint
app.MapGet("/swagger/v1/swagger.json", () =>
{
    var swaggerDoc = new
    {
        openapi = "3.0.1",
        info = new
        {
            title = "ApiTestingMcp Target Web API",
            version = "v1",
            description = "Sample Target Web API for MCP testing and endpoint discovery"
        },
        paths = new Dictionary<string, object>
        {
            ["/api/users"] = new
            {
                get = new
                {
                    tags = new[] { "Users" },
                    summary = "Get all users",
                    description = "Returns a list of all registered users.",
                    responses = new Dictionary<string, object>
                    {
                        ["200"] = new
                        {
                            description = "Success",
                            content = new
                            {
                                application_json = new
                                {
                                    schema = new
                                    {
                                        type = "array",
                                        items = new { @ref = "#/components/schemas/UserDto" }
                                    }
                                }
                            }
                        }
                    }
                },
                post = new
                {
                    tags = new[] { "Users" },
                    summary = "Create user",
                    description = "Creates a new user given a name and email.",
                    requestBody = new
                    {
                        required = true,
                        content = new Dictionary<string, object>
                        {
                            ["application/json"] = new
                            {
                                schema = new { @ref = "#/components/schemas/UserCreateModel" }
                            }
                        }
                    },
                    responses = new Dictionary<string, object>
                    {
                        ["201"] = new
                        {
                            description = "Created",
                            content = new Dictionary<string, object>
                            {
                                ["application/json"] = new
                                {
                                    schema = new { @ref = "#/components/schemas/UserDto" }
                                }
                            }
                        },
                        ["400"] = new
                        {
                            description = "Invalid input"
                        }
                    }
                }
            },
            ["/api/users/{id}"] = new
            {
                get = new
                {
                    tags = new[] { "Users" },
                    summary = "Get user by ID",
                    description = "Retrieves a user by their unique identifier.",
                    parameters = new[]
                    {
                        new
                        {
                            name = "id",
                            @in = "path",
                            required = true,
                            description = "Unique user ID",
                            schema = new { type = "integer", format = "int32" }
                        }
                    },
                    responses = new Dictionary<string, object>
                    {
                        ["200"] = new
                        {
                            description = "User found",
                            content = new Dictionary<string, object>
                            {
                                ["application/json"] = new
                                {
                                    schema = new { @ref = "#/components/schemas/UserDto" }
                                }
                            }
                        },
                        ["404"] = new { description = "User not found" }
                    }
                },
                delete = new
                {
                    tags = new[] { "Users" },
                    summary = "Delete user by ID",
                    description = "Deletes a user by their unique identifier.",
                    parameters = new[]
                    {
                        new
                        {
                            name = "id",
                            @in = "path",
                            required = true,
                            description = "Unique user ID",
                            schema = new { type = "integer", format = "int32" }
                        }
                    },
                    responses = new Dictionary<string, object>
                    {
                        ["204"] = new { description = "User deleted successfully" },
                        ["404"] = new { description = "User not found" }
                    }
                }
            }
        },
        components = new
        {
            schemas = new Dictionary<string, object>
            {
                ["UserDto"] = new
                {
                    type = "object",
                    required = new[] { "id", "name", "email" },
                    properties = new Dictionary<string, object>
                    {
                        ["id"] = new { type = "integer", format = "int32", description = "The user identifier" },
                        ["name"] = new { type = "string", description = "Full name of the user" },
                        ["email"] = new { type = "string", format = "email", description = "Email address" }
                    }
                },
                ["UserCreateModel"] = new
                {
                    type = "object",
                    required = new[] { "name", "email" },
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Full name of the user" },
                        ["email"] = new { type = "string", format = "email", description = "Email address" }
                    }
                }
            }
        }
    };

    var json = JsonSerializer.Serialize(swaggerDoc, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }).Replace("\"ref\":", "\"$ref\":").Replace("\"application_json\"", "\"application/json\"");

    return Results.Content(json, "application/json");
});

app.Run();

// DTO records for sample endpoints
public record UserDto(int Id, string Name, string Email);
public record UserCreateModel(string? Name, string? Email);
public record LoginRequestDto(string? Email, string? Password);
public record WebSetTargetRequest(string? BaseUrl, string? OpenApiPath);

// Preserved initial test tool
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