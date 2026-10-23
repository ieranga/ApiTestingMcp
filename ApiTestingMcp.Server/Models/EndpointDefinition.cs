namespace ApiTestingMcp.Server.Models;

public class EndpointSummary
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class EndpointDefinition
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<ParameterDefinition> Parameters { get; set; } = [];
    public RequestBodyDefinition? RequestBody { get; set; }
    public Dictionary<string, ResponseDefinition> Responses { get; set; } = [];
}

public class ParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string In { get; set; } = "query"; // path, query, header, cookie
    public bool Required { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public object? Schema { get; set; }
}

public class RequestBodyDefinition
{
    public string? Description { get; set; }
    public bool Required { get; set; }
    public string ContentType { get; set; } = "application/json";
    public object? Schema { get; set; }
}

public class ResponseDefinition
{
    public string StatusCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContentType { get; set; }
    public object? Schema { get; set; }
}
