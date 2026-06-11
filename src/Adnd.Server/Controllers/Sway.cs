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

        if (game.GMStatus != GMStatus.Running && game.GMStatus != GMStatus.Idle)
            return BadRequest(new { error = "Cannot sway: GM agent is not available." });

        // For Idle GM, Direction is optional (triggers auto-narrate)
        if (game.GMStatus == GMStatus.Idle && string.IsNullOrWhiteSpace(request.Direction))
            request.Direction = "Continue the narrative.";
        else if (string.IsNullOrWhiteSpace(request.Direction))
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
