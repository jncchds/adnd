using Adnd.Server.Data;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/llmlogs")]
[Authorize]
public class LLMLogsController(AppDbContext db, IUserIdProvider userIdProvider) : ControllerBase
{
    /// <summary>List LLM interaction logs for the current user (paginated).</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? gameId,
        [FromQuery] string? presetName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = userIdProvider.GetUserId();

        var query = db.LLMInteractionLogs.Where(l => l.UserId == userId);

        if (gameId.HasValue)
            query = query.Where(l => l.OriginGameId == gameId.Value);

        if (!string.IsNullOrEmpty(presetName))
            query = query.Where(l => l.PresetName == presetName);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { items, total });
    }

    /// <summary>Get a single LLM interaction log by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();
        var log = await db.LLMInteractionLogs
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, ct);

        if (log == null) return NotFound();
        return Ok(log);
    }

    /// <summary>Delete a single LLM interaction log.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();
        var log = await db.LLMInteractionLogs
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, ct);

        if (log == null) return NotFound();
        db.LLMInteractionLogs.Remove(log);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Delete all logs for a specific game.</summary>
    [HttpDelete("game/{gameId:guid}")]
    public async Task<IActionResult> DeleteByGame(Guid gameId, CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();
        var logs = await db.LLMInteractionLogs
            .Where(l => l.UserId == userId && l.OriginGameId == gameId)
            .ToListAsync(ct);
        db.LLMInteractionLogs.RemoveRange(logs);
        await db.SaveChangesAsync(ct);
        return Ok(new { deleted = logs.Count });
    }

    /// <summary>Bulk delete LLM interaction logs older than a given date.</summary>
    [HttpDelete]
    public async Task<IActionResult> BulkDelete(
        [FromQuery] Guid? gameId,
        [FromQuery] DateTimeOffset? before,
        CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();

        var query = db.LLMInteractionLogs.Where(l => l.UserId == userId);

        if (gameId.HasValue)
            query = query.Where(l => l.OriginGameId == gameId.Value);

        if (before.HasValue)
            query = query.Where(l => l.StartedAt < before.Value);

        var logs = await query.ToListAsync(ct);
        db.LLMInteractionLogs.RemoveRange(logs);
        await db.SaveChangesAsync(ct);
        return Ok(new { deleted = logs.Count });
    }

    /// <summary>Token usage stats for the current user.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats([FromQuery] Guid? gameId, CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();

        var query = db.LLMInteractionLogs.Where(l => l.UserId == userId);

        if (gameId.HasValue)
            query = query.Where(l => l.OriginGameId == gameId.Value);

        var stats = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCalls = g.Count(),
                TotalPromptTokens = g.Sum(l => l.PromptTokens),
                TotalCompletionTokens = g.Sum(l => l.CompletionTokens),
                TotalTokens = g.Sum(l => l.TotalTokens),
                TotalDurationMs = g.Sum(l => l.DurationMs),
                AverageDurationMs = g.Average(l => (double)l.DurationMs)
            })
            .FirstOrDefaultAsync(ct);

        return Ok(stats ?? new { TotalCalls = 0, TotalPromptTokens = 0, TotalCompletionTokens = 0, TotalTokens = 0, TotalDurationMs = 0L, AverageDurationMs = 0.0 });
    }
}
