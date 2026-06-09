using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Events;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== LLM Interaction Logs ====================

    [HttpGet("llm-interactions")]
    public async Task<IActionResult> GetLLMInteractions(
        [FromQuery] Guid? presetId,
        [FromQuery] Guid? gameId,
        [FromQuery] string? providerType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        List<LLMInteractionLog> logs;

        if (gameId.HasValue)
        {
            var game = await _context.Games.FindAsync(gameId.Value);
            if (game == null) return NotFound(new { error = "Game not found." });
            if (!await _authService.HasAccessAsync(_context, gameId.Value, _userIdProvider.GetCurrentUserId())) return Forbid();
            logs = await _interactionLogger.GetGameLogsAsync(gameId.Value, limit);
        }
        else
        {
            logs = await _interactionLogger.GetLogsAsync(userId, presetId, gameId, providerType, from, to, limit);
        }

        return Ok(logs.Select(l => new
        {
            l.Id,
            l.PresetId,
            PresetName = l.Preset?.Name,
            l.ProviderType,
            l.Model,
            l.PromptTokens,
            l.CompletionTokens,
            l.TotalTokens,
            l.DurationMs,
            l.Success,
            l.Error,
            l.SystemPrompt,
            l.UserPrompt,
            l.Response,
            l.Origin,
            l.OriginGameId,
            l.OriginSessionId,
            l.OriginAgent,
            l.OriginAction,
            l.StartedAt,
            l.CompletedAt
        }));
    }

    [HttpGet("llm-interactions/{logId}")]
    public async Task<IActionResult> GetLLMInteraction(Guid logId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var log = await _context.LLMInteractionLogs
            .Include(l => l.Preset)
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Id == logId);

        if (log == null) return NotFound(new { error = "Interaction log not found." });

        return Ok(new
        {
            log.Id,
            log.PresetId,
            PresetName = log.Preset?.Name,
            log.ProviderType,
            log.Model,
            log.PromptTokens,
            log.CompletionTokens,
            log.TotalTokens,
            log.DurationMs,
            log.Success,
            log.Error,
            log.SystemPrompt,
            log.UserPrompt,
            log.Response,
            log.RequestJson,
            log.ResponseJson,
            log.Origin,
            log.OriginGameId,
            log.OriginSessionId,
            log.OriginAgent,
            log.OriginAction,
            log.StartedAt,
            log.CompletedAt
        });
    }

    [HttpDelete("llm-interactions/{logId}")]
    public async Task<IActionResult> DeleteLLMInteraction(Guid logId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        await _interactionLogger.DeleteLogAsync(userId, logId);
        return Ok(new { message = "Interaction log deleted." });
    }

    [HttpGet("llm-interactions/preset-usage")]
    public async Task<IActionResult> GetPresetUsage(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var usage = await _interactionLogger.GetPresetUsageAsync(userId, from, to);
        return Ok(usage);
    }

    [HttpPost("llm-interactions/cleanup")]
    public async Task<IActionResult> CleanupOldLogs([FromBody] DateTime before)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        await _interactionLogger.DeleteOldLogsAsync(userId, before);
        return Ok(new { message = "Old logs cleaned up." });
    }

    [HttpGet("games/{gameId}/llm-provider-usage")]
    public async Task<IActionResult> GetGameProviderUsage(Guid gameId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var usage = await _interactionLogger.GetGameProviderUsageAsync(gameId, from, to);
        return Ok(usage);
    }

}
