using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Adnd.Server.Models;
using Adnd.Server.Data;
using Adnd.Server.Services;

namespace Adnd.Server.Controllers;

/// <summary>
/// Combat log history for a game.
/// Provides access to past combats and their event logs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CombatLogController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGameAuthorizationService _authService;
    private readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    private readonly ILogger<CombatLogController> _logger;

    public CombatLogController(
        AppDbContext context,
        IGameAuthorizationService authService,
        Adnd.Server.Services.IUserIdProvider userIdProvider,
        ILogger<CombatLogController> logger)
    {
        _context = context;
        _authService = authService;
        _userIdProvider = userIdProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get all combats for a game (past and active).
    /// </summary>
    [HttpGet("games/{gameId}/combats")]
    public async Task<IActionResult> GetCombats(Guid gameId, [FromQuery] int limit = 50)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var combats = await _context.Combats
            .Where(c => c.GameId == gameId)
            .OrderByDescending(c => c.StartedAt)
            .Take(limit)
            .ToListAsync();

        var results = new List<CombatSummaryEntry>();
        foreach (var c in combats)
        {
            results.Add(new CombatSummaryEntry
            {
                Id = c.Id.ToString(),
                Name = c.Name ?? "Unnamed Combat",
                Status = c.Status.ToString(),
                CurrentRound = c.CurrentRound,
                ParticipantCount = c.Participants.Count,
                EventCount = c.Events.Count,
                StartedAt = c.StartedAt,
                EndedAt = c.EndedAt,
                SessionId = c.SessionId,
                SessionTitle = c.Session != null ? c.Session.Title : null
            });
        }

        return Ok(new { gameId, count = combats.Count, combats });
    }

    /// <summary>
    /// Get a single combat with its full event log.
    /// </summary>
    [HttpGet("games/{gameId}/combats/{combatId}")]
    public async Task<IActionResult> GetCombat(Guid gameId, Guid combatId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var combat = await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .Include(c => c.Session)
            .FirstOrDefaultAsync(c => c.Id == combatId && c.GameId == gameId);

        if (combat == null) return NotFound(new { error = "Combat not found." });

        var participants = combat.Participants.Select(p => new ParticipantEntry
        {
            Id = p.Id.ToString(),
            DisplayName = p.DisplayName,
            ParticipantType = p.ParticipantType,
            CurrentHP = p.CurrentHP,
            MaxHP = p.MaxHP,
            AC = p.AC,
            Initiative = p.Initiative,
            Conditions = p.Conditions,
            DeathSaveState = p.DeathSaveState,
            Notes = p.Notes
        }).ToList();

        var events = combat.Events.Select(e => new CombatEventEntry
        {
            Id = e.Id.ToString(),
            Round = e.Round,
            TurnIndex = e.TurnIndex,
            Type = e.Type.ToString(),
            ActorName = e.ActorName,
            TargetName = e.TargetName,
            Content = e.Content,
            Metadata = e.Metadata,
            CreatedAt = e.CreatedAt
        }).ToList();

        return Ok(new
        {
            combat.Id,
            combat.Name,
            combat.Status,
            combat.CurrentRound,
            combat.CurrentTurnIndex,
            combat.StartedAt,
            combat.EndedAt,
            combat.SessionId,
            SessionTitle = combat.Session != null ? combat.Session.Title : null,
            Participants = participants,
            Events = events
        });
    }
}

public class CombatSummaryEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentRound { get; set; }
    public int ParticipantCount { get; set; }
    public int EventCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public Guid? SessionId { get; set; }
    public string? SessionTitle { get; set; }
}

public class ParticipantEntry
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int AC { get; set; }
    public int Initiative { get; set; }
    public JsonElement Conditions { get; set; }
    public JsonElement? DeathSaveState { get; set; }
    public JsonElement? Notes { get; set; }
}

public class CombatEventEntry
{
    public string Id { get; set; } = string.Empty;
    public int Round { get; set; }
    public int TurnIndex { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public JsonElement? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}
