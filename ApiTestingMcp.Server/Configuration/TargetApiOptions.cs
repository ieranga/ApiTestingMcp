namespace ApiTestingMcp.Server.Configuration;

public class TargetApiOptions
{
    public const string SectionName = "TargetApi";

    public string BaseUrl { get; set; } = "http://localhost:3001/";
    public string OpenApiPath { get; set; } = "/swagger/v1/swagger.json";
    public int TimeoutSeconds { get; set; } = 30;
}
