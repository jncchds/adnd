using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Whisper Management ====================

    [HttpGet("games/{gameId}/whispers")]
    public async Task<IActionResult> GetWhispers(Guid gameId, [FromQuery] int limit = 50)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);

        var isCreator = player?.Role == PlayerRole.Creator;
        var whispers = await _whisperService.GetWhispersForPlayerAsync(gameId, player!.Id, isCreator, limit);

        return Ok(whispers.Select(w => new
        {
            w.Id,
            FromPlayerId = w.FromPlayerId,
            FromCharacter = w.FromPlayer?.CharacterName ?? "Unknown",
            FromRole = w.FromPlayer?.Role ?? PlayerRole.Player,
            w.Content,
            w.Type,
            w.Targets,
            w.CreatedAt
        }));
    }

    [HttpPost("games/{gameId}/whispers")]
    public async Task<IActionResult> SendWhisper(Guid gameId, [FromBody] SendWhisperRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && p.Status == PlayerStatus.Active);

        if (player == null)
            return BadRequest(new { error = "You are not an active player in this game." });

        if (!_whisperService.CanWhisper(player))
            return StatusCode(403, new { error = "You do not have whisper permission." });

        try
        {
            var whisper = await _whisperService.SendWhisperAsync(
                gameId, request.SessionId, player.Id, request.Targets, request.Type, request.Content);

            return Ok(new { whisper.Id, whisper.Content, whisper.Type, whisper.Targets, whisper.CreatedAt });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("games/{gameId}/whispers/gm")]
    public async Task<IActionResult> SendGMWhisper(Guid gameId, Guid sessionId, [FromBody] SendGMWhisperRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && p.Status == PlayerStatus.Active);

        if (player == null || player.Role != PlayerRole.Creator)
            return StatusCode(403, new { error = "Only the game creator can send creator whispers." });

        var whisper = await _whisperService.SendGMWhisperAsync(
            gameId, sessionId, player.Id, request.TargetPlayerIds, request.Type, request.Content);

        return Ok(new { whisper.Id, whisper.Content, whisper.Type, whisper.Targets, whisper.CreatedAt });
    }

}
