using SurrealDb.Net;
using Microsoft.Extensions.Options;
using DotnetSample.Configuration;
using DotnetSample.Models;

namespace DotnetSample.Services;

public class VectorSearchService
{
    private readonly ISurrealDbClient _db;
    private readonly OllamaEmbeddingService _embeddingService;
    private readonly OllamaSettings _settings;

    public VectorSearchService(
        ISurrealDbClient db,
        OllamaEmbeddingService embeddingService,
        IOptions<OllamaSettings> settings)
    {
        _db = db;
        _embeddingService = embeddingService;
        _settings = settings.Value;
    }

    public async Task<Document> AddDocumentAsync(string content, Dictionary<string, object>? metadata = null)
    {
        var embedding = await _embeddingService.GetEmbeddingAsync(content);

        var doc = new Document
        {
            content = content,
            embedding = embedding,
            metadata = metadata ?? new Dictionary<string, object>(),
            createdAt = DateTime.UtcNow
        };

        var result = await _db.Insert<Document>("documents", doc);
        return result.FirstOrDefault() ?? throw new Exception("Failed to insert document");
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 5)
    {
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

        var sql = @"
            SELECT 
                *,
                vector::similarity::cosine(embedding, $embedding) AS similarity
            FROM documents
            WHERE embedding IS NOT NONE
            ORDER BY similarity DESC
            LIMIT $limit
        ";

        var parameters = new Dictionary<string, object>
        {
            { "embedding", queryEmbedding },
            { "limit", limit }
        };

        var response = await _db.Query(sql, parameters);
        var results = response.GetValue<List<SearchResult>>(0);

        return results ?? new List<SearchResult>();
    }

    public async Task InitializeAsync()
    {
        var dimension = _settings.EmbeddingDimension;

        await _db.RawQuery($@"
            DEFINE TABLE IF NOT EXISTS documents SCHEMALESS;
            DEFINE FIELD IF NOT EXISTS content ON documents TYPE string;
            DEFINE FIELD IF NOT EXISTS embedding ON documents TYPE array<float>;
            DEFINE FIELD IF NOT EXISTS metadata ON documents TYPE object;
            DEFINE FIELD IF NOT EXISTS createdAt ON documents TYPE datetime;
            DEFINE INDEX IF NOT EXISTS idx_documents_embedding 
                ON documents FIELDS embedding 
                MTREE DIMENSION {dimension} DIST COSINE;
        ");
    }
}