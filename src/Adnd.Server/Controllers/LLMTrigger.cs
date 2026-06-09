using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using System.Text.Json;
using Adnd.Server.Services;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Manual LLM Trigger Endpoints ====================

    [HttpPost("games/{gameId}/trigger/narrate")]
    public async Task<IActionResult> TriggerNarrate(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.GM,
            Action = AgentAction.Narrate,
            Input = JsonSerializer.Serialize(new GMDispatchOptions
            {
                SystemPrompt = $"You are the Game Master for a TTRPG session. " +
                    $"The creator has manually requested a narrative continuation. " +
                    $"Generate an engaging scene that advances the story naturally. " +
                    $"Consider plot threads, NPC actions, and player opportunities. " +
                    $"Be vivid and immersive. " +
                    $"Game system: {game.SystemId}. " +
                    $"Plot seed: {game.PlotSeed ?? "None"}. " +
                    $"Current game state: {game.GameState ?? "None"}.",
                UserPrompt = "Generate an engaging narrative continuation."
            }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Manual narrative triggered for game {GameId} by {UserId}", gameId, _userIdProvider.GetCurrentUserId());

        return Ok(new { call.Id, call.Status, call.CreatedAt, message = "Narrative queued" });
    }

    [HttpPost("games/{gameId}/trigger/suggest")]
    public async Task<IActionResult> TriggerSuggest(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.GM,
            Action = AgentAction.Suggest,
            Input = JsonSerializer.Serialize(new GMDispatchOptions
            {
                SystemPrompt = "You are a creative TTRPG Game Master assistant. " +
                    "Provide engaging plot suggestions based on the current game context. " +
                    "Respond with a JSON array of 3-5 actionable plot suggestions.",
                UserPrompt = "Suggest plot continuations based on the current game state."
            }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Manual suggestion triggered for game {GameId} by {UserId}", gameId, _userIdProvider.GetCurrentUserId());

        return Ok(new { call.Id, call.Status, call.CreatedAt, message = "Suggestions queued" });
    }

    [HttpPost("games/{gameId}/trigger/consistency")]
    public async Task<IActionResult> TriggerConsistency(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.RAG,
            Action = AgentAction.Check,
            Input = JsonSerializer.Serialize(new RAGDispatchOptions { MessageCount = 50 }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Manual consistency check triggered for game {GameId} by {UserId}", gameId, _userIdProvider.GetCurrentUserId());

        return Ok(new { call.Id, call.Status, call.CreatedAt, message = "Consistency check queued" });
    }

    [HttpPost("games/{gameId}/trigger/review")]
    public async Task<IActionResult> TriggerReview(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var review = await _plotWeaver.ReviewAndAdaptAsync(gameId, "Manual trigger from Game Room", "ManualReview");

        return Ok(new { review.Id, review.Trigger, review.Summary, review.Updates, review.ReviewedAt });
    }

    [HttpPost("games/{gameId}/trigger")]
    public async Task<IActionResult> TriggerGeneric(Guid gameId, [FromBody] TriggerRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.GM,
            Action = request.Action,
            Input = request.Input,
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Manual trigger ({Action}) for game {GameId} by {UserId}", request.Action, gameId, _userIdProvider.GetCurrentUserId());

        return Ok(new { call.Id, call.Status, call.CreatedAt, message = "Trigger queued" });
    }

    [HttpPost("games/{gameId}/agent-calls")]
    public async Task<IActionResult> CreateAgentCall(Guid gameId, [FromBody] CreateAgentCallRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = request.SessionId,
            FromAgent = request.FromAgent,
            ToAgent = request.ToAgent,
            Action = request.Action,
            Input = request.Input,
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _agentBus.SendCallAsync(call);

        return Ok(new
        {
            result.Id,
            result.FromAgent,
            result.ToAgent,
            result.Action,
            result.Status,
            result.CreatedAt
        });
    }

}
