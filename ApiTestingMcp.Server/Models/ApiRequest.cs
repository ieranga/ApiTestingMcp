namespace ApiTestingMcp.Server.Models;

public class ApiRequest
{
    public string Method { get; set; } = "GET";
    public string Endpoint { get; set; } = string.Empty;
    public string? Body { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? QueryParameters { get; set; }
    public bool UseAuth { get; set; } = true;
    public string? AuthProfile { get; set; }
}
