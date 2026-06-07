using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly ILLMProviderRegistry _providerRegistry;
    private readonly IAgentBus _agentBus;
    private readonly IWhisperService _whisperService;
    private readonly ILLMPresetService _presetService;
    private readonly ILLMInteractionLogger _interactionLogger;
    private readonly IPlotWeaver _plotWeaver;
    private readonly IUserIdProvider _userIdProvider;
    private readonly IGameAuthorizationService _authService;
    private readonly IMediator _mediator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext context,
        IGameEngine gameEngine,
        IRAGService ragService,
        ILLMProviderRegistry providerRegistry,
        IAgentBus agentBus,
        IWhisperService whisperService,
        ILLMPresetService presetService,
        ILLMInteractionLogger interactionLogger,
        IPlotWeaver plotWeaver,
        IUserIdProvider userIdProvider,
        IGameAuthorizationService authService,
        IMediator mediator,
        ILogger<AdminController> logger)
    {
        _context = context;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _providerRegistry = providerRegistry;
        _agentBus = agentBus;
        _whisperService = whisperService;
        _presetService = presetService;
        _interactionLogger = interactionLogger;
        _plotWeaver = plotWeaver;
        _userIdProvider = userIdProvider;
        _authService = authService;
        _mediator = mediator;
        _logger = logger;
    }

    // ==================== NPC Management ====================

    [HttpGet("games/{gameId}/npcs")]
    public async Task<IActionResult> GetNPCs(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });

        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var npcs = await _context.NPCs
            .Where(n => n.GameId == gameId)
            .Select(n => new
            {
                n.Id,
                n.Name,
                n.Description,
                n.Attributes,
                n.Skills,
                n.Inventory,
                n.Spells,
                n.PlotThreadId,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(npcs);
    }

    [HttpPost("games/{gameId}/npcs")]
    public async Task<IActionResult> CreateNPC(Guid gameId, [FromBody] CreateNPCRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var npc = new NPC
        {
            GameId = gameId,
            Name = request.Name,
            Description = request.Description,
            Attributes = request.Attributes ?? default,
            Skills = request.Skills ?? default,
            Inventory = request.Inventory ?? default,
            Spells = request.Spells ?? default,
            PlotThreadId = request.PlotThreadId,
            CreatedAt = DateTime.UtcNow
        };

        _context.NPCs.Add(npc);
        await _context.SaveChangesAsync();

        return Ok(new { npc.Id, npc.Name, npc.Description });
    }

    [HttpPut("npcs/{npcId}")]
    public async Task<IActionResult> UpdateNPC(Guid npcId, [FromBody] UpdateNPCRequest request)
    {
        var npc = await _context.NPCs.FindAsync(npcId);
        if (npc == null) return NotFound(new { error = "NPC not found." });

        var game = await _context.Games.FindAsync(npc.GameId);
        if (game == null || game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        if (request.Name != null) npc.Name = request.Name;
        if (request.Description != null) npc.Description = request.Description;
        if (request.Attributes != null) npc.Attributes = request.Attributes.GetValueOrDefault();
        if (request.Skills != null) npc.Skills = request.Skills.GetValueOrDefault();
        if (request.Inventory != null) npc.Inventory = request.Inventory.GetValueOrDefault();
        if (request.Spells != null) npc.Spells = request.Spells.GetValueOrDefault();
        if (request.PlotThreadId != null) npc.PlotThreadId = request.PlotThreadId;

        await _context.SaveChangesAsync();
        return Ok(new { npc.Id, npc.Name, npc.Description });
    }

    [HttpDelete("npcs/{npcId}")]
    public async Task<IActionResult> DeleteNPC(Guid npcId)
    {
        var npc = await _context.NPCs.FindAsync(npcId);
        if (npc == null) return NotFound(new { error = "NPC not found." });

        var game = await _context.Games.FindAsync(npc.GameId);
        if (game == null || game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        _context.NPCs.Remove(npc);
        await _context.SaveChangesAsync();

        return Ok(new { message = "NPC deleted." });
    }

    // ==================== Plot Thread Management ====================

    [HttpGet("games/{gameId}/plot-threads")]
    public async Task<IActionResult> GetPlotThreads(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var threads = await _plotWeaver.GetActiveThreadsAsync(gameId);

        return Ok(threads.Select(t => new
        {
            t.Id,
            t.Title,
            t.Category,
            t.Description,
            t.Status,
            t.Momentum,
            t.RelevanceScore,
            t.NextMilestone,
            t.Foreshadowing,
            t.AdaptationHistory,
            t.CreatedAt,
            t.UpdatedAt
        }));
    }

    [HttpPost("games/{gameId}/plot-threads")]
    public async Task<IActionResult> CreatePlotThread(Guid gameId, [FromBody] CreatePlotThreadRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var thread = new PlotThread
        {
            GameId = gameId,
            Title = request.Title,
            Description = request.Description,
            Status = PlotThreadStatus.Active,
            KeyEventMessageIds = new List<Guid>(),
            CreatedAt = DateTime.UtcNow
        };

        _context.PlotThreads.Add(thread);
        await _context.SaveChangesAsync();

        return Ok(new { thread.Id, thread.Title, thread.Description, thread.Status });
    }

    [HttpPut("plot-threads/{threadId}")]
    public async Task<IActionResult> UpdatePlotThread(Guid threadId, [FromBody] UpdatePlotThreadRequest request)
    {
        var thread = await _context.PlotThreads.FindAsync(threadId);
        if (thread == null) return NotFound(new { error = "Plot thread not found." });

        var game = await _context.Games.FindAsync(thread.GameId);
        if (game == null || game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        if (request.Title != null) thread.Title = request.Title;
        if (request.Description != null) thread.Description = request.Description;
        if (request.Status != null) thread.Status = (PlotThreadStatus)request.Status;
        if (request.KeyEventMessageIds != null) thread.KeyEventMessageIds = request.KeyEventMessageIds;
        thread.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { thread.Id, thread.Title, thread.Description, thread.Status });
    }

    [HttpPost("plot-threads/{threadId}/add-event")]
    public async Task<IActionResult> AddKeyEvent(Guid threadId, [FromBody] AddKeyEventRequest request)
    {
        var thread = await _context.PlotThreads.FindAsync(threadId);
        if (thread == null) return NotFound(new { error = "Plot thread not found." });

        if (!thread.KeyEventMessageIds.Contains(request.MessageId))
        {
            thread.KeyEventMessageIds.Add(request.MessageId);
            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Event added to plot thread." });
    }

    // ==================== Character Management ====================

    [HttpGet("games/{gameId}/characters")]
    public async Task<IActionResult> GetCharacters(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var characters = await _context.Characters
            .Where(c => c.Player!.GameId == gameId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Class,
                c.Level,
                c.ProficiencyBonus,
                c.CurrentHP,
                c.MaxHP,
                c.Attributes,
                c.Skills,
                c.Inventory,
                c.Spells,
                c.Conditions,
                c.CustomFields,
                c.UpdatedAt,
                PlayerName = c.Player!.User != null ? (c.Player.User!.DisplayName ?? c.Player.CharacterName) : c.Player.CharacterName
            })
            .ToListAsync();

        return Ok(characters);
    }

    [HttpGet("characters/{characterId}")]
    public async Task<IActionResult> GetCharacter(Guid characterId)
    {
        var character = await _context.Characters
            .Include(c => c.Player)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == characterId);

        if (character == null) return NotFound(new { error = "Character not found." });

        var playerId = character.Player?.GameId;
        if (playerId == null) return NotFound(new { error = "Character player not found." });

        var game = await _context.Games.FindAsync(playerId.Value);
        if (game == null || !await _authService.HasAccessAsync(_context, playerId.Value, _userIdProvider.GetCurrentUserId())) return Forbid();

        return Ok(new
        {
            character.Id,
            character.Name,
            character.Class,
            character.Level,
            character.CurrentHP,
            character.MaxHP,
            character.Attributes,
            character.Skills,
            character.Inventory,
            character.Spells,
            character.Conditions,
            character.CustomFields,
            character.UpdatedAt,
            PlayerName = character.Player.User?.DisplayName ?? character.Player.CharacterName
        });
    }

    [HttpPut("characters/{characterId}")]
    public async Task<IActionResult> UpdateCharacter(Guid characterId, [FromBody] UpdateCharacterRequest request)
    {
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Character not found." });

        var game = await _context.Games.FindAsync(character.Player!.GameId);
        if (game == null || !await _authService.HasAccessAsync(_context, character.Player.GameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        if (request.Name != null) character.Name = request.Name;
        if (request.Class != null) character.Class = request.Class;
        if (request.Level != null)
        {
            character.Level = (int)request.Level;
            character.ProficiencyBonus = GetProficiencyBonus((int)request.Level);
        }
        if (request.CurrentHP != null) character.CurrentHP = (int)request.CurrentHP;
        if (request.MaxHP != null) character.MaxHP = (int)request.MaxHP;
        if (request.Attributes != null) character.Attributes = request.Attributes.GetValueOrDefault();
        if (request.Skills != null) character.Skills = request.Skills.GetValueOrDefault();
        if (request.Inventory != null) character.Inventory = request.Inventory.GetValueOrDefault();
        if (request.Spells != null) character.Spells = request.Spells.GetValueOrDefault();
        if (request.Conditions != null) character.Conditions = request.Conditions.GetValueOrDefault();
        character.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { character.Id, character.Name, character.Class, character.Level });
    }

    // ==================== Game Management ====================

    [HttpPost("games/{gameId}/start")]
    public async Task<IActionResult> StartGame(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        if (game.LLMPresetId == null)
            return BadRequest(new { error = "Cannot start: no LLM preset configured. Set one in game creation." });

        game.Status = GameStatus.Active;
        game.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Publish game started event → triggers GameAgent activation
        await _mediator.Publish(new GameStarted(gameId, userId));

        return Ok(new { game.Id, game.Status, game.StartedAt });
    }

    [HttpPost("games/{gameId}/archive")]
    public async Task<IActionResult> ArchiveGame(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        game.Status = GameStatus.Archived;
        game.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Publish game archived event → triggers GameAgent pause
        await _mediator.Publish(new GameArchived(gameId));

        return Ok(new { game.Id, game.Status, game.EndedAt });
    }

    // ==================== GM Agent Status ====================

    [HttpGet("games/{gameId}/gm-status")]
    public async Task<IActionResult> GetGMStatus(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        return Ok(new {
            gameId,
            Status = game.GMStatus,
            game.LastGMAction,
            game.LastGMActionAt
        });
    }

    [HttpPost("games/{gameId}/gm/pause")]
    public async Task<IActionResult> PauseGM(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        game.GMStatus = GMStatus.Paused;
        game.LastGMAction = "Paused";
        game.LastGMActionAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _mediator.Publish(new GamePaused(gameId));
        return Ok(new { gameId, status = GMStatus.Paused });
    }

    [HttpPost("games/{gameId}/gm/resume")]
    public async Task<IActionResult> ResumeGM(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        game.GMStatus = GMStatus.Running;
        game.LastGMAction = "Resumed";
        game.LastGMActionAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _mediator.Publish(new GameResumed(gameId));
        return Ok(new { gameId, status = GMStatus.Running });
    }

    // ==================== Narrative Sway ====================

    [HttpPost("games/{gameId}/sway")]
    public async Task<IActionResult> SwayStory(Guid gameId, [FromBody] SwayRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        if (game.GMStatus != GMStatus.Running)
            return BadRequest(new { error = "Cannot sway: GM agent is not running." });

        if (string.IsNullOrWhiteSpace(request.Direction))
            return BadRequest(new { error = "Direction is required." });

        // Queue the sway event for the game agent to process
        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.GM,
            Action = AgentAction.Nudge,
            Input = request.Direction,
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        return Ok(new { gameId, call.Id, call.Status, call.CreatedAt });
    }

    // ==================== LLM / RAG Endpoints ====================

    [HttpGet("llm/providers")]
    public async Task<IActionResult> GetLLMProviders()
    {
        var statuses = _providerRegistry.GetAllStatusAsync().ToList();
        return Ok(statuses);
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

    // ==================== System Registry ====================

    [HttpGet("systems")]
    public async Task<IActionResult> GetSystems()
    {
        var systems = _context.CustomSystems
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.JsonDefinition,
                s.CreatedAt
            })
            .ToList();

        return Ok(systems);
    }

    [HttpPost("systems")]
    public async Task<IActionResult> CreateSystem(Guid gameId, [FromBody] CreateSystemRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var system = new CustomSystemDefinition
        {
            GameId = gameId,
            Name = request.Name,
            JsonDefinition = request.JsonDefinition,
            CreatedAt = DateTime.UtcNow
        };

        _context.CustomSystems.Add(system);
        await _context.SaveChangesAsync();

        return Ok(new { system.Id, system.Name, system.CreatedAt });
    }

    // ==================== Whisper Management ====================

    [HttpGet("games/{gameId}/whispers")]
    public async Task<IActionResult> GetWhispers(Guid gameId, [FromQuery] int limit = 50)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);

        var isCreator = player?.Role == PlayerRole.Creator;
        var whispers = await _whisperService.GetWhispersForPlayerAsync(gameId, player!.Id, isCreator, limit);

        return Ok(whispers.Select(w => new
        {
            w.Id,
            FromPlayerId = w.FromPlayerId,
            FromCharacter = w.FromPlayer?.CharacterName ?? "Unknown",
            FromRole = w.FromPlayer?.Role ?? PlayerRole.Player,
            w.Content,
            w.Type,
            w.Targets,
            w.CreatedAt
        }));
    }

    [HttpPost("games/{gameId}/whispers")]
    public async Task<IActionResult> SendWhisper(Guid gameId, [FromBody] SendWhisperRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && p.Status == PlayerStatus.Active);

        if (player == null)
            return BadRequest(new { error = "You are not an active player in this game." });

        if (!_whisperService.CanWhisper(player))
            return StatusCode(403, new { error = "You do not have whisper permission." });

        try
        {
            var whisper = await _whisperService.SendWhisperAsync(
                gameId, request.SessionId, player.Id, request.Targets, request.Type, request.Content);

            return Ok(new { whisper.Id, whisper.Content, whisper.Type, whisper.Targets, whisper.CreatedAt });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("games/{gameId}/whispers/gm")]
    public async Task<IActionResult> SendGMWhisper(Guid gameId, Guid sessionId, [FromBody] SendGMWhisperRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var userId = _userIdProvider.GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && p.Status == PlayerStatus.Active);

        if (player == null || player.Role != PlayerRole.Creator)
            return StatusCode(403, new { error = "Only the game creator can send creator whispers." });

        var whisper = await _whisperService.SendGMWhisperAsync(
            gameId, sessionId, player.Id, request.TargetPlayerIds, request.Type, request.Content);

        return Ok(new { whisper.Id, whisper.Content, whisper.Type, whisper.Targets, whisper.CreatedAt });
    }

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

    // ==================== Game State ====================

    [HttpGet("games/{gameId}/state")]
    public async Task<IActionResult> GetGameState(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        return Ok(new
        {
            gameId,
            GameState = game.GameState,
            PlotSeed = game.PlotSeed,
            GameParameters = game.GameParameters
        });
    }

    [HttpPut("games/{gameId}/state")]
    public async Task<IActionResult> UpdateGameState(Guid gameId, [FromBody] UpdateGameStateRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });

        var userId = _userIdProvider.GetCurrentUserId();
        if (game.CreatorId != userId)
            return Forbid();

        if (request.GameState != null) game.GameState = request.GameState;
        if (request.PlotSeed != null) game.PlotSeed = request.PlotSeed;
        if (request.GameParameters != null) game.GameParameters = request.GameParameters;

        await _context.SaveChangesAsync();

        return Ok(new { gameId, game.GameState, game.PlotSeed, game.GameParameters });
    }

    // ==================== LLM Presets ====================

    [HttpGet("llm-presets")]
    public async Task<IActionResult> GetLLMPresets()
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var presets = await _presetService.GetUserPresetsAsync(userId);
        return Ok(presets.Select(p => new
        {
            p.Id,
            p.Name,
            p.ProviderType,
            p.BaseModel,
            p.EndpointUrl,
            HasApiKey = !string.IsNullOrEmpty(p.ApiKey),
            p.Temperature,
            p.MaxTokens,
            p.TopP,
            p.IsDefault,
            p.IsActive,
            p.CreatedAt,
            p.UpdatedAt
        }));
    }

    [HttpGet("llm-presets/{presetId}")]
    public async Task<IActionResult> GetLLMPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var preset = await _presetService.GetPresetAsync(userId, presetId);
        if (preset == null) return NotFound(new { error = "Preset not found." });

        return Ok(new
        {
            preset.Id,
            preset.Name,
            preset.ProviderType,
            preset.BaseModel,
            preset.EndpointUrl,
            HasApiKey = !string.IsNullOrEmpty(preset.ApiKey),
            preset.Temperature,
            preset.MaxTokens,
            preset.TopP,
            preset.FrequencyPenalty,
            preset.PresencePenalty,
            preset.Stream,
            preset.EmbeddingModel,
            preset.EmbeddingEndpointUrl,
            preset.IsDefault,
            preset.IsActive,
            preset.ExtraParams,
            preset.CreatedAt,
            preset.UpdatedAt
        });
    }

    [HttpPost("llm-presets")]
    public async Task<IActionResult> CreateLLMPreset([FromBody] CreateLLMPresetRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var preset = await _presetService.CreatePresetAsync(userId, request);
        return Ok(new { preset.Id, preset.Name, preset.ProviderType, preset.BaseModel });
    }

    [HttpPut("llm-presets/{presetId}")]
    public async Task<IActionResult> UpdateLLMPreset(Guid presetId, [FromBody] UpdateLLMPresetRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var preset = await _presetService.UpdatePresetAsync(userId, presetId, request);
        return Ok(new { preset.Id, preset.Name, preset.ProviderType, preset.BaseModel });
    }

    [HttpDelete("llm-presets/{presetId}")]
    public async Task<IActionResult> DeleteLLMPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        await _presetService.DeletePresetAsync(userId, presetId);
        return Ok(new { message = "Preset deleted." });
    }

    [HttpPost("llm-presets/{presetId}/test")]
    public async Task<IActionResult> TestLLMPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        try
        {
            var isConnected = await _presetService.TestConnectionAsync(userId, presetId);
            return Ok(new { success = isConnected, message = isConnected ? "Connection successful" : "Connection failed" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("llm-presets/{presetId}/set-default")]
    public async Task<IActionResult> SetDefaultPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        await _presetService.SetDefaultPresetAsync(userId, presetId);
        return Ok(new { message = "Default preset updated." });
    }

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

    // ==================== PlotWeaver ====================

    [HttpPost("games/{gameId}/plot-weaver/review")]
    public async Task<IActionResult> TriggerReview(Guid gameId, [FromBody] string? context = null)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var reviewContext = context ?? "Manual review triggered by creator.";
        var review = await _plotWeaver.ReviewAndAdaptAsync(gameId, reviewContext, "ManualReview");

        return Ok(new
        {
            review.Id,
            review.Trigger,
            review.Summary,
            review.Updates,
            review.ReviewedAt
        });
    }

    [HttpGet("games/{gameId}/plot-weaver/reviews")]
    public async Task<IActionResult> GetReviewHistory(Guid gameId, [FromQuery] int limit = 20)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var reviews = await _plotWeaver.GetReviewHistoryAsync(gameId, limit);

        return Ok(reviews.Select(r => new
        {
            r.Id,
            r.Trigger,
            r.Summary,
            r.Updates,
            r.ReviewedAt
        }));
    }

    [HttpPost("games/{gameId}/plot-weaver/threads/{threadId}/momentum")]
    public async Task<IActionResult> AdjustMomentum(Guid gameId, Guid threadId, [FromBody] AdjustMomentumRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        await _plotWeaver.UpdateMomentumAsync(gameId, threadId, request.Delta, request.Reason);

        return Ok(new { message = "Momentum adjusted." });
    }

    [HttpPost("games/{gameId}/plot-weaver/opportunities")]
    public async Task<IActionResult> DetectOpportunities(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        // Gather recent context
        var recentMessages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();

        var context = string.Join("\n", recentMessages.Select(m =>
            $"[{m.CreatedAt:HH:mm}] {m.Player?.CharacterName ?? "System"}: {m.Content}"));

        var opportunities = await _plotWeaver.DetectOpportunitiesAsync(gameId, context);

        // Auto-apply non-complex opportunities
        foreach (var opp in opportunities)
        {
            try
            {
                switch (opp.Type)
                {
                    case OpportunityType.NewThread:
                        await _plotWeaver.GenerateNewThreadsAsync(gameId, context, "ManualOpportunity");
                        break;
                    case OpportunityType.SpawnMilestone:
                        var milestones = await _plotWeaver.SpawnMilestonesAsync(gameId);
                        foreach (var m in milestones)
                        {
                            _logger.LogInformation("Manual milestone triggered: {Title} in game {GameId}",
                                m.Title, gameId);
                        }
                        break;
                    case OpportunityType.EscalateThreat:
                        if (opp.MomentumDelta.HasValue && opp.ThreadId != null)
                        {
                            await _plotWeaver.UpdateMomentumAsync(gameId, Guid.Parse(opp.ThreadId),
                                opp.MomentumDelta.Value, opp.Title);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-apply opportunity in game {GameId}", gameId);
            }
        }

        return Ok(opportunities.Select(o => new
        {
            o.Type,
            o.Title,
            o.Description,
            o.ThreadId,
            o.MomentumDelta,
            o.NewThreadCategory,
            o.NewThreadTitle,
            o.NewThreadDescription,
            o.NewMilestone
        }));
    }

    // ==================== Helpers ====================

    private static int GetProficiencyBonus(int level)
    {
        if (level <= 4) return 2;
        if (level <= 8) return 3;
        if (level <= 12) return 4;
        if (level <= 16) return 5;
        return 6;
    }
}

// Request DTOs
public class CreateNPCRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonElement? Attributes { get; set; }
    public JsonElement? Skills { get; set; }
    public JsonElement? Inventory { get; set; }
    public JsonElement? Spells { get; set; }
    public Guid? PlotThreadId { get; set; }
}

public class UpdateNPCRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public JsonElement? Attributes { get; set; }
    public JsonElement? Skills { get; set; }
    public JsonElement? Inventory { get; set; }
    public JsonElement? Spells { get; set; }
    public Guid? PlotThreadId { get; set; }
}

public class CreatePlotThreadRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UpdatePlotThreadRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? Status { get; set; }
    public List<Guid>? KeyEventMessageIds { get; set; }
}

public class AddKeyEventRequest
{
    public Guid MessageId { get; set; }
}

public class UpdateCharacterRequest
{
    public string? Name { get; set; }
    public string? Class { get; set; }
    public int? Level { get; set; }
    public int? CurrentHP { get; set; }
    public int? MaxHP { get; set; }
    public JsonElement? Attributes { get; set; }
    public JsonElement? Skills { get; set; }
    public JsonElement? Inventory { get; set; }
    public JsonElement? Spells { get; set; }
    public JsonElement? Conditions { get; set; }
}

public class CreateSystemRequest
{
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
}

public class SimilarThreadRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 5;
}

// ============= Whisper DTOs =============

public class SendWhisperRequest
{
    public Guid SessionId { get; set; }
    public string Targets { get; set; } = string.Empty; // "player:{id}", "all", "group:{name}"
    public Adnd.Server.Models.WhisperType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class SendGMWhisperRequest
{
    public List<Guid> TargetPlayerIds { get; set; } = new();
    public Adnd.Server.Models.WhisperType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

// ============= Agent DTOs =============

public class CreateAgentCallRequest
{
    public Guid? SessionId { get; set; }
    public AgentType FromAgent { get; set; }
    public AgentType ToAgent { get; set; }
    public AgentAction Action { get; set; }
    public string? Input { get; set; }
}

// ============= Game State DTOs =============

public class UpdateGameStateRequest
{
    public string? GameState { get; set; }
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
}

// ============= PlotWeaver DTOs =============

public class AdjustMomentumRequest
{
    public float Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// ============= Sway DTO =============

public class SwayRequest
{
    public string Direction { get; set; } = string.Empty;
}
