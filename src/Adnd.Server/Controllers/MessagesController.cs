using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

/// <summary>
/// Chat history for a session. The client has always called this endpoint; it simply did
/// not exist, so the chat log rendered permanently empty and paged into a 404.
/// </summary>
[ApiController]
[Route("api/games/sessions")]
[Authorize]
public class MessagesController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : ControllerBase
{
    private const int MaxPageSize = 100;

    [HttpGet("{sessionId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid sessionId,
        [FromQuery] int limit = 30,
        [FromQuery] DateTimeOffset? cursor = null,
        CancellationToken ct = default)
    {
        var session = await db.GameSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
            return NotFound(new { error = "Session not found." });

        var userId = userIdProvider.GetUserId();

        Player player;
        try
        {
            player = await auth.RequirePlayerAsync(session.GameId, userId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }

        limit = Math.Clamp(limit, 1, MaxPageSize);

        var query = db.Messages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            // Any message carrying whisper routing is private to its sender/recipient —
            // keyed off the routing fields themselves, not a literal Type == "Whisper" check,
            // so a private GM-suggest reply (Type "GM" with WhisperToId set to the asker) is
            // hidden from everyone else the same way a player-to-player whisper is.
            .Where(m => (m.WhisperFromId == null && m.WhisperToId == null)
                || m.WhisperFromId == player.Id || m.WhisperToId == player.Id)
            // A secret roll is secret from the other players, not from the GM. It carries no
            // whisper routing on purpose — that would hide it from the AI narrator too, and
            // the narrator is exactly who is meant to see it.
            .Where(m => !m.IsSecret || m.PlayerId == player.Id || player.Role == PlayerRole.Creator);

        if (cursor is not null)
            query = query.Where(m => m.CreatedAt < cursor);

        // Fetch newest-first for the cursor walk, then hand back oldest-first for display.
        var page = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = page.Count > limit;
        var items = page.Take(limit).ToList();

        // One lookup for the authors on this page, rather than a join per row.
        var playerIds = items.Where(m => m.PlayerId.HasValue).Select(m => m.PlayerId!.Value).Distinct().ToList();
        var names = await db.Players
            .AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.User.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);

        var dtos = items
            .AsEnumerable()
            .Reverse()
            .Select(m => MessageHistoryDto.From(
                m,
                m.PlayerId.HasValue && names.TryGetValue(m.PlayerId.Value, out var n) ? n : null))
            .ToList();

        return Ok(new MessagePageDto(
            dtos,
            hasMore,
            hasMore ? items[^1].CreatedAt : null));
    }
}
