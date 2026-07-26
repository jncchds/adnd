using Adnd.Server.Data;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/dice")]
[Authorize]
public class DiceHistoryController(AppDbContext db, IUserIdProvider userIdProvider) : ControllerBase
{
    /// <summary>Returns all dice roll messages for a game.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] Guid gameId, CancellationToken ct)
    {
        var rolls = await db.Messages
            .Where(m => m.SessionId != Guid.Empty && m.Type == "DiceRoll")
            .Join(db.GameSessions.Where(s => s.GameId == gameId),
                  m => m.SessionId,
                  s => s.Id,
                  (m, s) => m)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        return Ok(rolls);
    }

    /// <summary>Returns dice roll messages scoped to the current user's player.</summary>
    [HttpGet("my-rolls")]
    public async Task<IActionResult> GetMyRolls([FromQuery] Guid gameId, CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();

        var player = await db.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId, ct);

        if (player == null) return Ok(Array.Empty<object>());

        var rolls = await db.Messages
            .Where(m => m.PlayerId == player.Id && m.Type == "DiceRoll")
            .Join(db.GameSessions.Where(s => s.GameId == gameId),
                  m => m.SessionId,
                  s => s.Id,
                  (m, s) => m)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        return Ok(rolls);
    }
}
