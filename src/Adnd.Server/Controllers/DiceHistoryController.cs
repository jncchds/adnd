using Adnd.Server.Data;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[Route("api/dice")]
public class DiceHistoryController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    /// <summary>Returns all dice roll messages for a game.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (await RequireMember(gameId) is { } failure) return failure;

        var rolls = await db.Messages
            .AsNoTracking()
            .Where(m => m.Type == "DiceRoll")
            .Join(db.GameSessions.Where(s => s.GameId == gameId),
                  m => m.SessionId, s => s.Id, (m, s) => m)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        return Ok(rolls);
    }

    /// <summary>Returns dice roll messages scoped to the current user's player.</summary>
    [HttpGet("my-rolls")]
    public async Task<IActionResult> GetMyRolls([FromQuery] Guid gameId, CancellationToken ct)
    {
        var (player, failure) = await ResolvePlayer(gameId);
        if (failure is not null) return failure;

        var rolls = await db.Messages
            .AsNoTracking()
            .Where(m => m.PlayerId == player!.Id && m.Type == "DiceRoll")
            .Join(db.GameSessions.Where(s => s.GameId == gameId),
                  m => m.SessionId, s => s.Id, (m, s) => m)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        return Ok(rolls);
    }
}
