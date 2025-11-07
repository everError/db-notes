namespace DotnetSample.Configuration;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimension { get; set; }
}