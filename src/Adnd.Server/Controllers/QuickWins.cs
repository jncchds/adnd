using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Hubs;

namespace Adnd.Server.Controllers;

/// <summary>
/// Quick-win feature endpoints: session notes, dice stats, message pagination,
/// message search, prompt templates, and SQLite fallback configuration.
/// </summary>
public partial class AdminController
{
    // ==================== Session Notes ====================

    /// <summary>
    /// Get all session notes for a session (GM/Creator only).
    /// </summary>
    [HttpGet("games/{gameId}/sessions/{sessionId}/notes")]
    public async Task<IActionResult> GetSessionNotes(Guid gameId, Guid sessionId)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var notes = await _sessionNoteService.GetNotesAsync(gameId, sessionId);
        return Ok(new { gameId, sessionId, notes });
    }

    /// <summary>
    /// Create a session note.
    /// </summary>
    [HttpPost("games/{gameId}/sessions/{sessionId}/notes")]
    public async Task<IActionResult> CreateSessionNote(Guid gameId, Guid sessionId, [FromBody] CreateSessionNoteRequest request)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var note = await _sessionNoteService.CreateNoteAsync(gameId, sessionId, userId, request.Title, request.Content);
        return Ok(note);
    }

    /// <summary>
    /// Update a session note.
    /// </summary>
    [HttpPut("games/{gameId}/notes/{noteId}")]
    public async Task<IActionResult> UpdateSessionNote(Guid gameId, Guid noteId, [FromBody] UpdateSessionNoteRequest request)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var note = await _sessionNoteService.UpdateNoteAsync(gameId, noteId, userId, request.Title, request.Content);
        return Ok(note);
    }

    /// <summary>
    /// Delete a session note (creator only).
    /// </summary>
    [HttpDelete("games/{gameId}/notes/{noteId}")]
    public async Task<IActionResult> DeleteSessionNote(Guid gameId, Guid noteId)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        await _sessionNoteService.DeleteNoteAsync(gameId, noteId, userId);
        return Ok(new { message = "Session note deleted." });
    }

    // ==================== Dice Roll Statistics ====================

    /// <summary>
    /// Get dice roll statistics for a game (optional session filter).
    /// </summary>
    [HttpGet("games/{gameId}/dice-stats")]
    public async Task<IActionResult> GetDiceStats(Guid gameId, [FromQuery] Guid? sessionId)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var stats = await _diceStatsService.GetStatsAsync(gameId, sessionId);
        return Ok(stats);
    }

    /// <summary>
    /// Get dice roll statistics for a specific player.
    /// </summary>
    [HttpGet("games/{gameId}/dice-stats/player/{playerId}")]
    public async Task<IActionResult> GetPlayerDiceStats(Guid gameId, Guid playerId, [FromQuery] Guid? sessionId)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var stats = await _diceStatsService.GetPlayerStatsAsync(gameId, playerId, sessionId);
        return Ok(stats);
    }

    // ==================== Message Pagination ====================

    /// <summary>
    /// Paginated messages for a session.
    /// </summary>
    [HttpGet("games/{gameId}/sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetMessagesPaginated(
        Guid gameId,
        Guid sessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] MessageType? type = null)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var query = _context.Messages
            .Include(m => m.Player)
            .Include(m => m.Session)
            .Where(m => m.SessionId == sessionId);

        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);

        var ordered = query.OrderByDescending(m => m.CreatedAt);
        var total = await ordered.CountAsync();
        var messages = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.SessionId,
                PlayerId = m.PlayerId,
                PlayerName = m.Player != null ? (m.Player.CharacterName ?? "Unknown") : "System",
                Content = m.Content,
                Type = m.Type,
                IsOOC = m.IsOOC,
                Metadata = m.Metadata,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            gameId,
            sessionId,
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
            messages
        });
    }

    // ==================== Message Search (pgvector) ====================

    /// <summary>
    /// Search messages in a session using pgvector cosine similarity.
    /// Requires pgvector extension enabled.
    /// </summary>
    [HttpPost("games/{gameId}/sessions/{sessionId}/messages/search")]
    public async Task<IActionResult> SearchMessages(
        Guid gameId,
        Guid sessionId,
        [FromBody] MessageSearchRequest request)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        // Use pgvector cosine similarity via raw SQL
        var embeddingStr = string.Join(",", request.QueryEmbedding.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var sql = string.Format(
            "SELECT m.\"Id\", m.\"Content\", m.\"Type\", " +
            "COALESCE(p.\"CharacterName\", 'System') as \"PlayerName\", " +
            "m.\"IsOOC\", m.\"CreatedAt\", " +
            "(m.\"Embedding\" <=> array[{0}]) as \"Distance\" " +
            "FROM \"Messages\" m " +
            "LEFT JOIN \"Players\" p ON m.\"PlayerId\" = p.\"Id\" " +
            "WHERE m.\"SessionId\" = '{1}' " +
            "AND m.\"Embedding\" IS NOT NULL " +
            "ORDER BY \"Distance\" " +
            "LIMIT {2}",
            embeddingStr, sessionId, request.Limit);

        // Execute raw SQL and map to a simple result type
        var rawResults = await _context.Database
            .SqlQueryRaw<MessageSearchRawResult>(sql)
            .ToListAsync();

        var results = rawResults.Select(r => new
        {
            r.Id,
            Content = r.Content,
            Type = (MessageType)r.Type,
            PlayerName = r.PlayerName,
            IsOOC = r.IsOOC,
            CreatedAt = r.CreatedAt,
            Distance = r.Distance
        }).ToList();

        return Ok(new { gameId, sessionId, query = request.Query, results });
    }

    // ==================== Prompt Templates ====================

    /// <summary>
    /// Get all prompt templates for a game.
    /// </summary>
    [HttpGet("games/{gameId}/prompt-templates")]
    public async Task<IActionResult> GetPromptTemplates(Guid gameId)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var templates = await _promptTemplateService.GetTemplatesAsync(gameId);
        return Ok(templates);
    }

    /// <summary>
    /// Create a prompt template.
    /// </summary>
    [HttpPost("games/{gameId}/prompt-templates")]
    public async Task<IActionResult> CreatePromptTemplate(Guid gameId, [FromBody] CreatePromptTemplateRequest request)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _promptTemplateService.CreateTemplateAsync(gameId, userId, request.Name, request.Type, request.Prompt);
        return Ok(template);
    }

    /// <summary>
    /// Update a prompt template.
    /// </summary>
    [HttpPut("games/{gameId}/prompt-templates/{templateId}")]
    public async Task<IActionResult> UpdatePromptTemplate(
        Guid gameId,
        Guid templateId,
        [FromBody] UpdatePromptTemplateRequest request)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _promptTemplateService.UpdateTemplateAsync(
            gameId, templateId, userId,
            request.Name, request.Type, request.Prompt,
            request.IsActive, request.IsDefault);
        return Ok(template);
    }

    /// <summary>
    /// Delete a prompt template.
    /// </summary>
    [HttpDelete("games/{gameId}/prompt-templates/{templateId}")]
    public async Task<IActionResult> DeletePromptTemplate(Guid gameId, Guid templateId)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())
            || !await _authService.HasGmRoleAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        await _promptTemplateService.DeleteTemplateAsync(gameId, templateId, userId);
        return Ok(new { message = "Prompt template deleted." });
    }

    /// <summary>
    /// Get the default template for a given type.
    /// </summary>
    [HttpGet("games/{gameId}/prompt-templates/default/{type}")]
    public async Task<IActionResult> GetDefaultTemplate(Guid gameId, string type)
    {
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var template = await _promptTemplateService.GetDefaultTemplateAsync(gameId, type);
        return Ok(template);
    }
}

// ==================== Request DTOs for Quick-Win Features ====================

public class CreateSessionNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class UpdateSessionNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class CreatePromptTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "narration", "consistency", "review", "session_summary", "custom"
    public string Prompt { get; set; } = string.Empty;
}

public class UpdatePromptTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
    public bool? IsDefault { get; set; }
}

public class MessageSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public float[] QueryEmbedding { get; set; } = Array.Empty<float>();
    public int Limit { get; set; } = 10;
}

// Raw SQL result type for pgvector search
public class MessageSearchRawResult
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Type { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public bool IsOOC { get; set; }
    public DateTime CreatedAt { get; set; }
    public float Distance { get; set; }
}
