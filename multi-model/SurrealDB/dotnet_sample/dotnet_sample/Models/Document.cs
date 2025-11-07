namespace DotnetSample.Models;

public class Document
{
    public string? Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<float> Embedding { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class SearchResult : Document
{
    public double Similarity { get; set; }
}