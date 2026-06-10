using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Adnd.Server.Shared;
using Adnd.Server.Features.Games.Dto;

namespace Adnd.Server.Features.Games;

[ApiController]
[Route("api/systems")]
[Authorize]
public class SystemRegistryController : ControllerBase
{
    private readonly AppDbContext _db;

    public SystemRegistryController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var systems = await _db.GameSystems
            .OrderBy(s => s.Type)
            .ThenBy(s => s.Name)
            .ToListAsync();

        return Ok(systems.Select(MapToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGameSystemRequest request)
    {
        if (await _db.GameSystems.AnyAsync(s => s.Slug == request.Slug))
            return Conflict(new { error = "Slug already exists" });

        var system = new GameSystem
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            RulesetConfig = request.RulesetConfig,
            Type = SystemType.Custom,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.GameSystems.Add(system);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = system.Id }, MapToResponse(system));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var system = await _db.GameSystems.FindAsync(id);
        if (system == null) return NotFound();

        return Ok(MapToResponse(system));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGameSystemRequest request)
    {
        var system = await _db.GameSystems.FindAsync(id);
        if (system == null) return NotFound();
        if (system.Type == SystemType.Predefined)
            return BadRequest(new { error = "Cannot modify predefined systems" });

        system.Name = request.Name;
        system.Description = request.Description;
        system.RulesetConfig = request.RulesetConfig;
        system.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(MapToResponse(system));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var system = await _db.GameSystems.FindAsync(id);
        if (system == null) return NotFound();
        if (system.Type == SystemType.Predefined)
            return BadRequest(new { error = "Cannot delete predefined systems" });

        _db.GameSystems.Remove(system);
        await _db.SaveChangesAsync();

        return Ok();
    }

    private GameSystemResponse MapToResponse(GameSystem system) => new()
    {
        Id = system.Id,
        Name = system.Name,
        Slug = system.Slug,
        Description = system.Description,
        Type = system.Type.ToString().ToLower(),
        RulesetConfig = system.RulesetConfig,
        CreatedAt = system.CreatedAt,
        UpdatedAt = system.UpdatedAt
    };
}
