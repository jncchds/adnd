using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== System Registry ====================

    [HttpGet("systems")]
    public async Task<IActionResult> GetSystems()
    {
        var systems = _context.CustomSystems
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.JsonDefinition,
                s.CreatedAt
            })
            .ToList();

        return Ok(systems);
    }

    [HttpPost("systems")]
    public async Task<IActionResult> CreateSystem(Guid gameId, [FromBody] CreateSystemRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var system = new CustomSystemDefinition
        {
            GameId = gameId,
            Name = request.Name,
            JsonDefinition = request.JsonDefinition,
            CreatedAt = DateTime.UtcNow
        };

        _context.CustomSystems.Add(system);
        await _context.SaveChangesAsync();

        return Ok(new { system.Id, system.Name, system.CreatedAt });
    }

}
