using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Game Management ====================

    [HttpPost("games/{gameId}/start")]
    public async Task<IActionResult> StartGame(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var result = await _gameStartService.StartGameAsync(gameId, userId);

        if (!result.Success)
        {
            return result.Error switch
            {
                "Game not found." => NotFound(new { error = result.Error }),
                "Forbidden." => Forbid(),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return Ok(new { result.GameId, result.Status, result.StartedAt, result.PlotThreadsGenerated });
    }

    [HttpPost("games/{gameId}/archive")]
    public async Task<IActionResult> ArchiveGame(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        game.Status = GameStatus.Archived;
        game.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Publish game archived event → triggers GameAgent pause
        await _mediator.Publish(new GameArchived(gameId));

        return Ok(new { game.Id, game.Status, game.EndedAt });
    }
}
