using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Data;
using Adnd.Server.Services;

namespace Adnd.Server.Controllers;

/// <summary>
/// Dice roll history for a game.
/// Queries Message entities with MessageType.Dice and returns structured roll data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DiceHistoryController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGameAuthorizationService _authService;
    private readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    private readonly ILogger<DiceHistoryController> _logger;

    public DiceHistoryController(
        AppDbContext context,
        IGameAuthorizationService authService,
        Adnd.Server.Services.IUserIdProvider userIdProvider,
        ILogger<DiceHistoryController> logger)
    {
        _context = context;
        _authService = authService;
        _userIdProvider = userIdProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get dice roll history for a game, filtered by session and player.
    /// </summary>
    [HttpGet("games/{gameId}/dice-history")]
    public async Task<IActionResult> GetDiceHistory(
        Guid gameId,
        [FromQuery] Guid? sessionId,
        [FromQuery] Guid? playerId,
        [FromQuery] int limit = 100,
        [FromQuery] string? sortBy = "desc")
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var messages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId && m.Type == MessageType.Dice)
            .OrderByDescending(m => m.CreatedAt)
            .Where(m => !sessionId.HasValue || m.SessionId == sessionId.Value)
            .Where(m => !playerId.HasValue || m.PlayerId == playerId.Value)
            .Take(limit)
            .ToListAsync();

        var results = new List<DiceHistoryEntry>();
        foreach (var msg in messages)
        {
            var metadata = msg.Metadata;
            results.Add(new DiceHistoryEntry
            {
                Id = msg.Id.ToString(),
                Formula = metadata.TryGetProperty("formula", out var fProp) && fProp.ValueKind == System.Text.Json.JsonValueKind.String ? fProp.GetString() ?? "" : "",
                Total = metadata.TryGetProperty("total", out var tProp) && tProp.ValueKind == System.Text.Json.JsonValueKind.Number ? (int?)tProp.GetInt32() : null,
                DiceCount = metadata.TryGetProperty("diceCount", out var dcProp) && dcProp.ValueKind == System.Text.Json.JsonValueKind.Number ? (int?)dcProp.GetInt32() : null,
                DiceType = metadata.TryGetProperty("diceType", out var dtProp) && dtProp.ValueKind == System.Text.Json.JsonValueKind.Number ? (int?)dtProp.GetInt32() : null,
                Modifier = metadata.TryGetProperty("modifier", out var mod) && mod.ValueKind == System.Text.Json.JsonValueKind.Number ? mod.GetInt32() : 0,
                Rolls = metadata.TryGetProperty("rolls", out var rolls) && rolls.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? rolls.EnumerateArray().Select(r => r.GetInt32()).ToList()
                    : new List<int>(),
                SessionId = msg.SessionId,
                SessionTitle = msg.Session?.Title,
                PlayerId = msg.PlayerId,
                CharacterName = msg.Player?.CharacterName,
                CreatedAt = msg.CreatedAt
            });
        }

        if (sortBy == "asc")
            results.Reverse();

        return Ok(new { gameId, count = results.Count, rolls = results });
    }

    /// <summary>
    /// Get a single dice roll by message ID.
    /// </summary>
    [HttpGet("games/{gameId}/dice-history/{messageId}")]
    public async Task<IActionResult> GetDiceRoll(Guid gameId, Guid messageId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var msg = await _context.Messages
            .Include(m => m.Session)
            .Include(m => m.Player)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Session!.GameId == gameId && m.Type == MessageType.Dice);

        if (msg == null) return NotFound(new { error = "Dice roll not found." });

        var metadata = msg.Metadata;
        var entry = new DiceHistoryEntry
        {
            Id = msg.Id.ToString(),
            Formula = metadata.TryGetProperty("formula", out var fProp) && fProp.ValueKind == System.Text.Json.JsonValueKind.String ? fProp.GetString() ?? "" : "",
            Total = metadata.TryGetProperty("total", out var tProp) && tProp.ValueKind == System.Text.Json.JsonValueKind.Number ? (int?)tProp.GetInt32() : null,
            DiceCount = metadata.TryGetProperty("diceCount", out var dcProp) && dcProp.ValueKind == System.Text.Json.JsonValueKind.Number ? (int?)dcProp.GetInt32() : null,
            DiceType = metadata.TryGetProperty("diceType", out var dtProp) && dtProp.ValueKind == System.Text.Json.JsonValueKind.Number ? (int?)dtProp.GetInt32() : null,
            Modifier = metadata.TryGetProperty("modifier", out var mod) && mod.ValueKind == System.Text.Json.JsonValueKind.Number ? mod.GetInt32() : 0,
            Rolls = metadata.TryGetProperty("rolls", out var rolls) && rolls.ValueKind == System.Text.Json.JsonValueKind.Array
                ? rolls.EnumerateArray().Select(r => r.GetInt32()).ToList()
                : new List<int>(),
            SessionId = msg.SessionId,
            SessionTitle = msg.Session?.Title,
            PlayerId = msg.PlayerId,
            CharacterName = msg.Player?.CharacterName,
            CreatedAt = msg.CreatedAt
        };

        return Ok(entry);
    }
}

public class DiceHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public int? Total { get; set; }
    public int? DiceCount { get; set; }
    public int? DiceType { get; set; }
    public int Modifier { get; set; }
    public List<int> Rolls { get; set; } = new();
    public Guid? SessionId { get; set; }
    public string? SessionTitle { get; set; }
    public Guid? PlayerId { get; set; }
    public string? CharacterName { get; set; }
    public DateTime CreatedAt { get; set; }
}
