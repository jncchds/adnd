using Adnd.Server.Data;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[Route("api/combatlog")]
public class CombatLogController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    /// <summary>List all combats for a game.</summary>
    [HttpGet]
    public async Task<IActionResult> ListCombats([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (await RequireMember(gameId) is { } failure) return failure;

        var combats = await db.Combats
            .AsNoTracking()
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
            .AsNoTracking()
            .Include(c => c.Participants)
            .Include(c => c.Events.OrderBy(e => e.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (combat == null) return NotFound();

        // Authorize against the combat's own game.
        if (await RequireMember(combat.GameId) is { } failure) return failure;

        return Ok(combat);
    }
}
