using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== LLM / RAG Endpoints ====================

    [HttpGet("llm/providers")]
    public async Task<IActionResult> GetLLMProviders()
    {
        var statuses = _providerRegistry.GetAllStatusAsync().ToList();
        return Ok(statuses);
    }

    [HttpGet("llm-presets/models")]
    public async Task<IActionResult> GetProviderModels([FromQuery] string providerType, [FromQuery] string? endpointUrl, [FromQuery] string? apiKey)
    {
        try
        {
            var models = await _presetService.GetAvailableModelsAsync(providerType, endpointUrl, apiKey);
            return Ok(models);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Ok(new { models = Array.Empty<string>(), error = ex.Message });
        }
    }

    [HttpGet("games/{gameId}/plot-context")]
    public async Task<IActionResult> GetPlotContext(Guid gameId, [FromQuery] int maxMessages = 20)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var context = await _ragService.GeneratePlotContextAsync(gameId, maxMessages);
        return Ok(new { context });
    }

    [HttpPost("games/{gameId}/rag/similar-threads")]
    public async Task<IActionResult> FindSimilarThreads(Guid gameId, [FromBody] SimilarThreadRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var threads = await _ragService.FindSimilarPlotThreadsAsync(gameId, request.Query, request.Limit);

        return Ok(threads.Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.Status,
            t.CreatedAt
        }));
    }

    [HttpGet("games/{gameId}/rag/consistency")]
    public async Task<IActionResult> CheckConsistency(Guid gameId, [FromQuery] int messageCount = 50)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var report = await _ragService.CheckPlotConsistencyAsync(gameId, messageCount);
        return Ok(report);
    }

    [HttpGet("games/{gameId}/rag/summary")]
    public async Task<IActionResult> GetSessionSummary(Guid gameId, [FromQuery] Guid sessionId, [FromQuery] int messageCount = 30)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var summary = await _ragService.GenerateSessionSummaryAsync(sessionId, messageCount);
        return Ok(new { summary });
    }

}
