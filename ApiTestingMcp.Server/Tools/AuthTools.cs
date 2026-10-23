using System.ComponentModel;
using System.Text.Json;
using ApiTestingMcp.Server.Models;
using ApiTestingMcp.Server.Services;
using ModelContextProtocol.Server;

namespace ApiTestingMcp.Server.Tools;

[McpServerToolType]
public class AuthTools
{
    private readonly AuthManagerService _authManager;
    private readonly ApiClientService _apiClient;

    public AuthTools(AuthManagerService authManager, ApiClientService apiClient)
    {
        _authManager = authManager;
        _apiClient = apiClient;
    }

    [McpServerTool(Name = "set_bearer_token")]
    [Description("Directly sets a JWT Bearer token for authentication. Decodes and displays token claims and expiration.")]
    public AuthStatus SetBearerToken(
        [Description("JWT token string (with or without 'Bearer ' prefix)")] string token,
        [Description("Profile name to store the token under. Defaults to 'default'.")] string profile = "default")
    {
        _authManager.SetBearerToken(token, profile);
        _authManager.SetActiveProfile(profile);
        return _authManager.GetStatus(profile);
    }

    [McpServerTool(Name = "login_api")]
    [Description("Executes a login request against the target API, automatically extracts the JWT token, stores it, and reports token claims.")]
    public async Task<LoginResult> LoginApi(
        [Description("Login endpoint relative path (e.g. '/api/Auth/login'). Default is '/api/Auth/login'.")] string endpoint = "/api/Auth/login",
        [Description("Optional raw JSON body to send. If not provided, username and password will be serialized.")] string? body = null,
        [Description("Optional email or username")] string? username = null,
        [Description("Optional password")] string? password = null,
        [Description("Profile name to save this session under (e.g. 'default', 'admin', 'user')")] string profile = "default",
        CancellationToken cancellationToken = default)
    {
        var targetEndpoint = string.IsNullOrWhiteSpace(endpoint) ? "/api/Auth/login" : endpoint.Trim();

        string requestBody;
        if (!string.IsNullOrWhiteSpace(body))
        {
            requestBody = body.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            requestBody = JsonSerializer.Serialize(new
            {
                email = username.Trim(),
                password = password
            });
        }
        else
        {
            return new LoginResult
            {
                Success = false,
                StatusCode = 400,
                Message = "Either a JSON 'body' or both 'username' and 'password' must be supplied."
            };
        }

        var apiRequest = new ApiRequest
        {
            Method = "POST",
            Endpoint = targetEndpoint,
            Body = requestBody,
            UseAuth = false // Do not attach auth headers to the login request itself
        };

        var response = await _apiClient.SendAsync(apiRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new LoginResult
            {
                Success = false,
                StatusCode = response.StatusCode,
                Message = $"Login failed with HTTP {response.StatusCode} ({response.ReasonPhrase}): {response.Body}",
                RawResponseBody = response.Body
            };
        }

        var token = AuthManagerService.TryExtractToken(response.Body ?? "");
        if (string.IsNullOrEmpty(token))
        {
            return new LoginResult
            {
                Success = false,
                StatusCode = response.StatusCode,
                Message = "Login response was HTTP 200/201, but could not automatically locate a JWT token in the response payload.",
                RawResponseBody = response.Body
            };
        }

        _authManager.SetBearerToken(token, profile);
        _authManager.SetActiveProfile(profile);

        var (claims, _) = AuthManagerService.DecodeJwtClaims(token);

        return new LoginResult
        {
            Success = true,
            StatusCode = response.StatusCode,
            Message = $"Login successful. Token saved to profile '{profile}'.",
            Profile = profile,
            TokenPreview = AuthManagerService.MaskToken(token),
            Claims = claims,
            RawResponseBody = response.Body
        };
    }

    [McpServerTool(Name = "get_auth_status")]
    [Description("Gets current authentication status, active profile, token expiration, and decoded JWT claims.")]
    public AuthStatus GetAuthStatus(
        [Description("Optional profile name to check. Defaults to active profile.")] string? profile = null)
    {
        return _authManager.GetStatus(profile);
    }

    [McpServerTool(Name = "set_api_key")]
    [Description("Sets an API Key header for authentication.")]
    public AuthStatus SetApiKey(
        [Description("Header name (e.g. 'X-Api-Key' or 'ApiKey')")] string headerName,
        [Description("API Key value")] string apiKey,
        [Description("Profile name. Defaults to 'default'.")] string profile = "default")
    {
        _authManager.SetApiKey(headerName, apiKey, profile);
        _authManager.SetActiveProfile(profile);
        return _authManager.GetStatus(profile);
    }

    [McpServerTool(Name = "clear_auth")]
    [Description("Clears stored authentication tokens and credentials for a profile or the active session.")]
    public AuthStatus ClearAuth(
        [Description("Optional profile to clear. If omitted, clears the active profile.")] string? profile = null)
    {
        _authManager.ClearAuth(profile);
        return _authManager.GetStatus(profile);
    }
}
