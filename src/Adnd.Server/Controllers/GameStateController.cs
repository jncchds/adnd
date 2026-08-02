using Adnd.Server.Data;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[Route("api/[controller]")]
public class GameStateController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpGet("{gameId}")]
    public async Task<IActionResult> Get(Guid gameId)
    {
        if (await RequireMember(gameId) is { } failure) return failure;

        var game = await db.Games.FindAsync(gameId);
        if (game == null) return NotFound();
        return Ok(new { gameId, state = game.GameState });
    }

    // Overwrites the whole serialized game state, so this is the GM's call alone.
    [HttpPut("{gameId}")]
    public async Task<IActionResult> Update(Guid gameId, [FromBody] UpdateStateRequest req)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        var game = await db.Games.FindAsync(gameId);
        if (game == null) return NotFound();
        game.GameState = req.State;
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { gameId, state = game.GameState });
    }
}

public record UpdateStateRequest(string State);
