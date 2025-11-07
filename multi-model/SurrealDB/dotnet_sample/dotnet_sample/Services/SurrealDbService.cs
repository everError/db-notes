using SurrealDb.Net;

namespace DotnetSample.Services;

public class SurrealDbService(ISurrealDbClient client)
{
    private readonly ISurrealDbClient _client = client;

    public ISurrealDbClient GetClient() => _client;
}