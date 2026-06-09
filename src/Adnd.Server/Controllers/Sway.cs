using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Narrative Sway ====================

    [HttpPost("games/{gameId}/sway")]
    public async Task<IActionResult> SwayStory(Guid gameId, [FromBody] SwayRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot sway: GM agent is not running." });

        if (string.IsNullOrWhiteSpace(request.Direction))
            return BadRequest(new { error = "Direction is required." });

        // Queue the sway event for the game agent to process
        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.GM,
            Action = AgentAction.Nudge,
            Input = request.Direction,
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        return Ok(new { gameId, call.Id, call.Status, call.CreatedAt });
    }

}
