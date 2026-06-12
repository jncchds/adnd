using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Events;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== GM Agent Status ====================

    [HttpGet("games/{gameId}/gm-status")]
    public async Task<IActionResult> GetGMStatus(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        return Ok(new {
            gameId,
            Status = game.GMStatus,
            game.LastGMAction,
            game.LastGMActionAt
        });
    }

    [HttpPost("games/{gameId}/gm/pause")]
    public async Task<IActionResult> PauseGM(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        game.GMStatus = GMStatus.Paused;
        game.LastGMAction = "Paused";
        game.LastGMActionAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new GamePaused(gameId));
        return Ok(new { gameId, status = GMStatus.Paused });
    }

    [HttpPost("games/{gameId}/gm/resume")]
    public async Task<IActionResult> ResumeGM(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        game.GMStatus = GMStatus.Running;
        game.LastGMAction = "Resumed";
        game.LastGMActionAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new GameResumed(gameId));
        return Ok(new { gameId, status = GMStatus.Running });
    }

}
