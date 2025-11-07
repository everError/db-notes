using Microsoft.Extensions.Options;
using DotnetSample.Configuration;

namespace DotnetSample.Services;

public class OllamaEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;

    public OllamaEmbeddingService(HttpClient httpClient, IOptions<OllamaSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var request = new
        {
            model = _settings.EmbeddingModel,
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        return result?.Embedding ?? throw new Exception("Failed to get embedding");
    }

    private class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = [];
    }
}