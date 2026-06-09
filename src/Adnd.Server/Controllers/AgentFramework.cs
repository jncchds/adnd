using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Agent Framework ====================

    [HttpGet("games/{gameId}/agent-calls")]
    public async Task<IActionResult> GetAgentCallHistory(Guid gameId,
        [FromQuery] AgentType? fromAgent, [FromQuery] AgentAction? action,
        [FromQuery] int limit = 50)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var calls = await _agentBus.GetCallHistoryAsync(gameId, fromAgent, action, limit);

        return Ok(calls.Select(c => new
        {
            c.Id,
            c.FromAgent,
            c.ToAgent,
            c.Action,
            c.Status,
            c.Output,
            c.OutputMessage,
            c.DurationMs,
            c.Error,
            c.CreatedAt,
            c.CompletedAt
        }));
    }

    [HttpGet("agent-calls/{callId}")]
    public async Task<IActionResult> GetAgentCall(Guid callId)
    {
        var call = await _agentBus.GetCallAsync(callId);
        if (call == null) return NotFound(new { error = "Agent call not found." });

        var game = await _context.Games.FindAsync(call.GameId);
        if (game == null)
            return NotFound(new { error = "Game not found." });

        var userId = _userIdProvider.GetCurrentUserId();
        var hasAccess = game.CreatorId == userId || game.Players.Any(p => p.UserId == userId);
        if (!hasAccess) return Forbid();

        return Ok(new
        {
            call.Id,
            call.FromAgent,
            call.ToAgent,
            call.Action,
            call.Status,
            call.Input,
            call.Output,
            call.OutputMessage,
            call.DurationMs,
            call.Error,
            call.CreatedAt,
            call.CompletedAt
        });
    }

    [HttpGet("games/{gameId}/agent-calls/pending")]
    public async Task<IActionResult> GetPendingAgentCalls(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var pendingCalls = await _context.AgentCalls
            .Where(c => c.GameId == gameId && (c.Status == AgentCallStatus.Pending || c.Status == AgentCallStatus.Running))
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.FromAgent,
                c.ToAgent,
                c.Action,
                c.Status,
                c.Input,
                c.CreatedAt,
                c.StartedAt,
                c.OutputMessage
            })
            .ToListAsync();

        return Ok(new
        {
            pendingCalls,
            pendingCount = pendingCalls.Count(c => c.Status == AgentCallStatus.Pending),
            runningCount = pendingCalls.Count(c => c.Status == AgentCallStatus.Running)
        });
    }

}
