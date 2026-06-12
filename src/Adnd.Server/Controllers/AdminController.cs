using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using Adnd.Server.Hubs;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public partial class AdminController : ControllerBase
{
    protected readonly AppDbContext _context;
    protected readonly IGameEngine _gameEngine;
    protected readonly IRAGService _ragService;
    protected readonly IAgentBus _agentBus;
    protected readonly IWhisperService _whisperService;
    protected readonly ILLMPresetService _presetService;
    protected readonly ILLMInteractionLogger _interactionLogger;
    protected readonly IPlotWeaver _plotWeaver;
    protected readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    protected readonly IGameAuthorizationService _authService;
    protected readonly IEventBus _eventBus;
    protected readonly IHubContext<GameHub> _hubContext;
    protected readonly ILogger<AdminController> _logger;
    protected readonly IGameStartService _gameStartService;

    // New services for quick-win features
    protected readonly ISessionNoteService _sessionNoteService;
    protected readonly IPromptTemplateService _promptTemplateService;
    protected readonly IDiceStatsService _diceStatsService;
    protected readonly IGameTemplateService _gameTemplateService;

    public AdminController(
        AppDbContext context,
        IGameEngine gameEngine,
        IRAGService ragService,
        IAgentBus agentBus,
        IWhisperService whisperService,
        ILLMPresetService presetService,
        ILLMInteractionLogger interactionLogger,
        IPlotWeaver plotWeaver,
        Adnd.Server.Services.IUserIdProvider userIdProvider,
        IGameAuthorizationService authService,
        IEventBus mediator,
        IHubContext<GameHub> hubContext,
        ILogger<AdminController> logger,
        IGameStartService gameStartService,
        ISessionNoteService sessionNoteService,
        IPromptTemplateService promptTemplateService,
        IDiceStatsService diceStatsService,
        IGameTemplateService gameTemplateService)
    {
        _context = context;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _agentBus = agentBus;
        _whisperService = whisperService;
        _presetService = presetService;
        _interactionLogger = interactionLogger;
        _plotWeaver = plotWeaver;
        _userIdProvider = userIdProvider;
        _authService = authService;
        _eventBus = mediator;
        _hubContext = hubContext;
        _logger = logger;
        _gameStartService = gameStartService;
        _sessionNoteService = sessionNoteService;
        _promptTemplateService = promptTemplateService;
        _diceStatsService = diceStatsService;
        _gameTemplateService = gameTemplateService;
    }

    /// <summary>
    /// Manually trigger game pause.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/pause")]
    public async Task<IActionResult> PauseGame(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        await _eventBus.PublishAsync(new GamePaused(gameId));

        return Ok(new { message = "Game paused event published" });
    }

    /// <summary>
    /// Manually trigger game resume.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/resume")]
    public async Task<IActionResult> ResumeGame(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        await _eventBus.PublishAsync(new GameResumed(gameId));

        return Ok(new { message = "Game resumed event published" });
    }

    /// <summary>
    /// Manually trigger a combat start simulation.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/combat-start")]
    public async Task<IActionResult> TriggerCombatStart(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var combat = new Combat
        {
            GameId = gameId,
            Name = "Simulated Combat",
            Status = CombatStatus.Active,
            StartedAt = DateTime.UtcNow,
            CurrentRound = 1,
            CurrentTurnIndex = 0,
        };

        _context.Combats.Add(combat);
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new CombatStarted(gameId, null, combat.Name));

        return Ok(new { combatId = combat.Id, message = "Simulated combat started" });
    }

    /// <summary>
    /// Manually trigger a combat end simulation for the most recent active combat.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/combat-end")]
    public async Task<IActionResult> TriggerCombatEnd(Guid gameId)
    {
        var combat = await _context.Combats
            .Where(c => c.GameId == gameId && c.Status == CombatStatus.Active)
            .OrderByDescending(c => c.StartedAt)
            .FirstOrDefaultAsync();

        if (combat == null) return BadRequest(new { error = "No active combat found to end." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        combat.Status = CombatStatus.Finished;
        combat.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new CombatEnded(gameId, combat.Id, "Simulation Ended"));

        return Ok(new { combatId = combat.Id, message = "Simulated combat ended" });
    }

    // ==================== Additional Trigger Endpoints ====================
    [HttpPost("games/{gameId}/trigger/generate-threads")]
    public async Task<IActionResult> TriggerGenerateThreads(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var recentMessages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();

        var context = string.Join("\n", recentMessages.Select(m =>
            $"[{m.CreatedAt:HH:mm}] {m.Player?.CharacterName ?? "System"}: {m.Content}"));

        var threads = await _plotWeaver.GenerateNewThreadsAsync(gameId, context, "ManualGenerateThreads");

        return Ok(new { threadCount = threads.Count, threads = threads.Select(t => new { t.Id, t.Title, t.Category, t.Description }) });
    }

    /// <summary>
    /// Manually trigger milestone spawning.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/spawn-milestones")]
    public async Task<IActionResult> TriggerSpawnMilestones(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var milestones = await _plotWeaver.SpawnMilestonesAsync(gameId);

        return Ok(new { milestoneCount = milestones.Count, milestones = milestones.Select(m => new { m.Id, m.Title, m.Description, m.Status }) });
    }

    /// <summary>
    /// Manually trigger session summary generation via RAG.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/session-summary")]
    public async Task<IActionResult> TriggerSessionSummary(Guid gameId, [FromBody] string? sessionId = null)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = string.IsNullOrEmpty(sessionId) ? null : Guid.Parse(sessionId),
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.RAG,
            Action = AgentAction.Generate,
            Input = JsonSerializer.Serialize(new RAGDispatchOptions { MessageCount = 30 }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _agentBus.SendCallAsync(call);

        return Ok(new { result.Id, result.Status, result.CreatedAt, message = "Session summary queued" });
    }

    /// <summary>
    /// Manually trigger plot opportunity detection.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/detect-opportunities")]
    public async Task<IActionResult> TriggerDetectOpportunities(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var recentMessages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();

        var context = string.Join("\n", recentMessages.Select(m =>
            $"[{m.CreatedAt:HH:mm}] {m.Player?.CharacterName ?? "System"}: {m.Content}"));

        var opportunities = await _plotWeaver.DetectOpportunitiesAsync(gameId, context);

        // Auto-apply
        foreach (var opp in opportunities)
        {
            try
            {
                switch (opp.Type)
                {
                    case OpportunityType.NewThread:
                        await _plotWeaver.GenerateNewThreadsAsync(gameId, context, "ManualDetectOpportunities");
                        break;
                    case OpportunityType.SpawnMilestone:
                        await _plotWeaver.SpawnMilestonesAsync(gameId);
                        break;
                    case OpportunityType.EscalateThreat:
                        if (opp.MomentumDelta.HasValue && opp.ThreadId != null)
                            await _plotWeaver.UpdateMomentumAsync(gameId, Guid.Parse(opp.ThreadId), opp.MomentumDelta.Value, opp.Title);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-apply opportunity in game {GameId}", gameId);
            }
        }

        return Ok(new { opportunityCount = opportunities.Count, opportunities = opportunities.Select(o => new { o.Type, o.Title, o.Description, o.ThreadId, o.MomentumDelta, o.NewThreadCategory, o.NewThreadTitle }) });
    }

    /// <summary>
    /// Manually trigger the GM agent to evaluate the current state (like a heartbeat).
    /// </summary>
    [HttpPost("games/{gameId}/trigger/gm-evaluate")]
    public async Task<IActionResult> TriggerGMEvaluate(Guid gameId)
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
                    $"The creator has manually requested a state evaluation. " +
                    $"Review the current game state, plot threads, and recent events. " +
                    $"Identify any pending plot threads that need attention, " +
                    $"any NPCs that should act, and any opportunities for story development. " +
                    $"Provide a concise evaluation and suggest the next narrative beat. " +
                    $"Game system: {game.SystemId}. " +
                    $"Current game state: {game.GameState ?? "None"}.",
                UserPrompt = "Evaluate the current game state and suggest the next narrative beat."
            }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _agentBus.SendCallAsync(call);

        return Ok(new { result.Id, result.Status, result.CreatedAt, message = "GM evaluation queued" });
    }

    /// <summary>
    /// Manually trigger the GM agent to generate a new scene.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/new-scene")]
    public async Task<IActionResult> TriggerNewScene(Guid gameId)
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
                    $"The creator has requested a new scene. " +
                    $"Create a vivid, immersive scene that advances the story. " +
                    $"Consider the current plot threads, character motivations, and world state. " +
                    $"Be creative and engaging. " +
                    $"Game system: {game.SystemId}.",
                UserPrompt = "Create a new scene that advances the story."
            }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _agentBus.SendCallAsync(call);

        return Ok(new { result.Id, result.Status, result.CreatedAt, message = "New scene queued" });
    }

    /// <summary>
    /// Manually trigger the GM agent to check for plot opportunities.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/plot-check")]
    public async Task<IActionResult> TriggerPlotCheck(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var recentMessages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();

        var context = string.Join("\n", recentMessages.Select(m =>
            $"[{m.CreatedAt:HH:mm}] {m.Player?.CharacterName ?? "System"}: {m.Content}"));

        var opportunities = await _plotWeaver.DetectOpportunitiesAsync(gameId, context);

        return Ok(new { opportunityCount = opportunities.Count, opportunities = opportunities.Select(o => new { o.Type, o.Title, o.Description, o.ThreadId, o.MomentumDelta }) });
    }

    /// <summary>
    /// Manually trigger a full plot review.
    /// </summary>
    [HttpPost("games/{gameId}/trigger/full-review")]
    public async Task<IActionResult> TriggerFullReview(Guid gameId, [FromBody] string? context = null)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();
        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot trigger: GM agent is not running." });

        var reviewContext = context ?? "Manual full review triggered by creator.";
        var review = await _plotWeaver.ReviewAndAdaptAsync(gameId, reviewContext, "ManualFullReview");

        return Ok(new { review.Id, review.Trigger, review.Summary, review.Updates, review.ReviewedAt });
    }

    // ==================== Game Templates ====================

    /// <summary>
    /// Create a new game template from the current game configuration.
    /// </summary>
    [HttpPost("game-templates")]
    public async Task<IActionResult> CreateGameTemplate([FromBody] CreateGameTemplateRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _gameTemplateService.CreateTemplateAsync(
            userId,
            request.Name,
            request.DefaultName,
            request.SystemId,
            request.LLMPresetId,
            request.LLMPresetName,
            request.Language,
            request.PlotSeed,
            request.GameParameters);
        return Ok(template);
    }

    /// <summary>
    /// Update an existing game template.
    /// </summary>
    [HttpPut("game-templates/{id}")]
    public async Task<IActionResult> UpdateGameTemplate(Guid id, [FromBody] UpdateGameTemplateRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _gameTemplateService.UpdateTemplateAsync(
            userId, id,
            request.Name,
            request.DefaultName,
            request.SystemId,
            request.LLMPresetId,
            request.LLMPresetName,
            request.Language,
            request.PlotSeed,
            request.GameParameters);
        return Ok(template);
    }

    /// <summary>
    /// Delete a game template (owner only).
    /// </summary>
    [HttpDelete("game-templates/{id}")]
    public async Task<IActionResult> DeleteGameTemplate(Guid id)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        try
        {
            await _gameTemplateService.DeleteTemplateAsync(userId, id);
            return Ok(new { message = "Template deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Template not found." });
        }
    }

    /// <summary>
    /// Get a single game template by ID (owner only).
    /// </summary>
    [HttpGet("game-templates/{id}")]
    public async Task<IActionResult> GetGameTemplate(Guid id)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _gameTemplateService.GetTemplateAsync(userId, id);
        if (template == null)
            return NotFound(new { error = "Template not found." });
        return Ok(template);
    }
}

// ==================== Request DTOs ====================

public class CreateGameTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DefaultName { get; set; }
    public string SystemId { get; set; } = "dnd5e";
    public Guid? LLMPresetId { get; set; }
    public string? LLMPresetName { get; set; }
    public string Language { get; set; } = "English";
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
}

public class UpdateGameTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DefaultName { get; set; }
    public string SystemId { get; set; } = "dnd5e";
    public Guid? LLMPresetId { get; set; }
    public string? LLMPresetName { get; set; }
    public string Language { get; set; } = "English";
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
}
