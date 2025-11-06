using SurrealDb.Net.Models;

namespace SurrealDbSampleApp.Models;

public class Person : Record
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Email { get; set; }
}