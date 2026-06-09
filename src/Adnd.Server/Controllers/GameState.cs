using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Game State ====================

    [HttpGet("games/{gameId}/state")]
    public async Task<IActionResult> GetGameState(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        return Ok(new
        {
            gameId,
            GameState = game.GameState,
            PlotSeed = game.PlotSeed,
            GameParameters = game.GameParameters
        });
    }

    [HttpPut("games/{gameId}/state")]
    public async Task<IActionResult> UpdateGameState(Guid gameId, [FromBody] UpdateGameStateRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });

        var userId = _userIdProvider.GetCurrentUserId();
        if (game.CreatorId != userId)
            return Forbid();

        if (request.GameState != null) game.GameState = request.GameState;
        if (request.PlotSeed != null) game.PlotSeed = request.PlotSeed;
        if (request.GameParameters != null) game.GameParameters = request.GameParameters;

        await _context.SaveChangesAsync();

        return Ok(new { gameId, game.GameState, game.PlotSeed, game.GameParameters });
    }

}
