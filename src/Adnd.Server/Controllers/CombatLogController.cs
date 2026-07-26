using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/combatlog")]
[Authorize]
public class CombatLogController(AppDbContext db) : ControllerBase
{
    /// <summary>List all combats for a game.</summary>
    [HttpGet]
    public async Task<IActionResult> ListCombats([FromQuery] Guid gameId, CancellationToken ct)
    {
        var combats = await db.Combats
            .Where(c => c.GameId == gameId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return Ok(combats);
    }

    /// <summary>Get full combat detail including participants and events.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCombat(Guid id, CancellationToken ct)
    {
        var combat = await db.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events.OrderBy(e => e.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (combat == null) return NotFound();
        return Ok(combat);
    }
}
