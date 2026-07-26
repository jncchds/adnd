using Adnd.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameStateController(AppDbContext db) : ControllerBase
{
    [HttpGet("{gameId}")]
    public async Task<IActionResult> Get(Guid gameId)
    {
        var game = await db.Games.FindAsync(gameId);
        if (game == null) return NotFound();
        return Ok(new { gameId, state = game.GameState });
    }

    [HttpPut("{gameId}")]
    public async Task<IActionResult> Update(Guid gameId, [FromBody] UpdateStateRequest req)
    {
        var game = await db.Games.FindAsync(gameId);
        if (game == null) return NotFound();
        game.GameState = req.State;
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { gameId, state = game.GameState });
    }
}

public record UpdateStateRequest(string State);
