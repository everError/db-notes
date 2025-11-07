using Microsoft.AspNetCore.Mvc;
using DotnetSample.Services;

namespace DotnetSample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VectorSearchController(VectorSearchService vectorSearch) : ControllerBase
{
    [HttpPost("documents")]
    public async Task<IActionResult> AddDocument([FromBody] AddDocumentRequest request)
    {
        var document = await vectorSearch.AddDocumentAsync(request.Content, request.Metadata);
        return Ok(document);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        var results = await vectorSearch.SearchAsync(request.Query, request.Limit ?? 5);
        return Ok(results);
    }
}

public class AddDocumentRequest
{
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int? Limit { get; set; }
}