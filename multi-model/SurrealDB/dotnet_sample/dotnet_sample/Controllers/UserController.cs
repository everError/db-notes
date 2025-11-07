using Microsoft.AspNetCore.Mvc;
using DotnetSample.Models;
using DotnetSample.Services;
using SurrealDb.Net.Models;

namespace DotnetSample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(SurrealDbService db) : ControllerBase
{
    private readonly SurrealDbService _db = db;

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
    {
        var users = await _db.GetClient().Select<User>("user");
        return Ok(users);
    }

    // GET: api/users/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(string id)
    {
        var user = await _db.GetClient().Select<User>(("user", id));

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<User>> Create(User user)
    {
        var created = await _db.GetClient().Create("user", user);
        return CreatedAtAction(nameof(GetById), new { id = created?.Id }, created);
    }

    // PUT: api/users/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<User>> Update(string id, User user)
    {
        var target = await _db.GetClient().Select<User>(("user", id));
        if (target == null)
        {
            return NotFound();
        }
        // Merge 사용 - 전체 객체로 업데이트
        var updated = await _db.GetClient().Merge<User, User>(user);

        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    // PATCH: api/users/{id}
    [HttpPatch("{id}")]
    public async Task<ActionResult<User>> Patch(string id, Dictionary<string, object> updates)
    {
        // Merge 사용 - 특정 필드만 업데이트
        var updated = await _db.GetClient().Merge<User>(("user", id), updates);

        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _db.GetClient().Delete(("user", id));
        return NoContent();
    }

    // GET: api/users/search?name={name}
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<User>>> Search([FromQuery] string name)
    {
        var result = await _db.GetClient().Query($"SELECT * FROM user WHERE name CONTAINS {name}");
        var users = result.GetValue<List<User>>(0);

        return Ok(users);
    }
}