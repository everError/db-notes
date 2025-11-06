using SurrealDb.Net;
using SurrealDb.Net.Models.Auth;
using SurrealDbSampleApp.Models;
using System.Text.Json;

const string TABLE = "person";

// WS 클라이언트 생성 (Docker localhost:8000)
await using var db = new SurrealDbClient("ws://localhost:8000/rpc");

// 인증 (root/root – 실제로는 안전한 값으로 변경)
await db.SignIn(new RootAuth { Username = "root", Password = "root" });

// 네임스페이스/데이터베이스 사용
await db.Use("test", "test");

// 데이터 생성 (Create) – 반환된 Id는 RecordId 타입
var newPerson = new Person { Name = "Jane Doe", Age = 28, Email = "jane@example.com" };
var created = await db.Create(TABLE, newPerson);
var createdId = created.Id;  // RecordId 인스턴스 (e.g., person:uuid)
Console.WriteLine($"Created ID: {createdId} (Type: {createdId.GetType().Name})");
Console.WriteLine("Created: " + JsonSerializer.Serialize(created));

// 데이터 조회 (Select All) – people[0].Id도 RecordId
var people = await db.Select<Person>(TABLE);
Console.WriteLine("\nAll People:");
foreach (var person in people)
{
    Console.WriteLine($"ID: {person.Id}, Name: {person.Name}, Age: {person.Age}, Email: {person.Email}");
}

Console.WriteLine("\nDone! Press any key to exit.");
Console.ReadKey();