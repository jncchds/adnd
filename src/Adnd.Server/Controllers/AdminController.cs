using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;

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
        _logger = logger;
    }

    // ==================== NPC Management ====================

    [HttpGet("games/{gameId}/npcs")]
    public async Task<IActionResult> GetNPCs(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });

        if (!HasAccess(game)) return Forbid();

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
        if (game.CreatorId != GetCurrentUserId()) return Forbid();

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
        if (game == null || game.CreatorId != GetCurrentUserId()) return Forbid();

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
        if (game == null || game.CreatorId != GetCurrentUserId()) return Forbid();

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
        if (!HasAccess(game)) return Forbid();

        var threads = await _context.PlotThreads
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Description,
                p.Status,
                p.KeyEventMessageIds,
                p.CreatedAt,
                p.UpdatedAt
            })
            .ToListAsync();

        return Ok(threads);
    }

    [HttpPost("games/{gameId}/plot-threads")]
    public async Task<IActionResult> CreatePlotThread(Guid gameId, [FromBody] CreatePlotThreadRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != GetCurrentUserId()) return Forbid();

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
        if (game == null || game.CreatorId != GetCurrentUserId()) return Forbid();

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
        if (!HasAccess(game)) return Forbid();

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

        var game = await _context.Games.FindAsync(playerId);
        if (game == null || !HasAccess(game)) return Forbid();

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
        if (game == null || !HasAccess(game)) return Forbid();

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
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != GetCurrentUserId()) return Forbid();

        game.Status = GameStatus.Active;
        game.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { game.Id, game.Status, game.StartedAt });
    }

    [HttpPost("games/{gameId}/archive")]
    public async Task<IActionResult> ArchiveGame(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != GetCurrentUserId()) return Forbid();

        game.Status = GameStatus.Archived;
        game.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { game.Id, game.Status, game.EndedAt });
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
        if (!HasAccess(game)) return Forbid();

        var context = await _ragService.GeneratePlotContextAsync(gameId, maxMessages);
        return Ok(new { context });
    }

    [HttpPost("games/{gameId}/rag/similar-threads")]
    public async Task<IActionResult> FindSimilarThreads(Guid gameId, [FromBody] SimilarThreadRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!HasAccess(game)) return Forbid();

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
        if (!HasAccess(game)) return Forbid();

        var report = await _ragService.CheckPlotConsistencyAsync(gameId, messageCount);
        return Ok(report);
    }

    [HttpGet("games/{gameId}/rag/summary")]
    public async Task<IActionResult> GetSessionSummary(Guid gameId, [FromQuery] Guid sessionId, [FromQuery] int messageCount = 30)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!HasAccess(game)) return Forbid();

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
        if (game.CreatorId != GetCurrentUserId()) return Forbid();

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
        if (!HasAccess(game)) return Forbid();

        var userId = GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);

        var isGM = player?.Role == PlayerRole.GM;
        var whispers = await _whisperService.GetWhispersForPlayerAsync(gameId, player!.Id, isGM, limit);

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
        if (!HasAccess(game)) return Forbid();

        var userId = GetCurrentUserId();
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
        if (!HasAccess(game)) return Forbid();

        var userId = GetCurrentUserId();
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && p.Status == PlayerStatus.Active);

        if (player == null || player.Role != PlayerRole.GM)
            return StatusCode(403, new { error = "Only the GM can send GM whispers." });

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
        if (!HasAccess(game)) return Forbid();

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

        var userId = GetCurrentUserId();
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
        if (!HasAccess(game)) return Forbid();

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
        if (!HasAccess(game)) return Forbid();

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

        var userId = GetCurrentUserId();
        if (game.CreatorId != userId && game.GameMasterId != userId)
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
        var userId = GetCurrentUserId();
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
        var userId = GetCurrentUserId();
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
        var userId = GetCurrentUserId();
        var preset = await _presetService.CreatePresetAsync(userId, request);
        return Ok(new { preset.Id, preset.Name, preset.ProviderType, preset.BaseModel });
    }

    [HttpPut("llm-presets/{presetId}")]
    public async Task<IActionResult> UpdateLLMPreset(Guid presetId, [FromBody] UpdateLLMPresetRequest request)
    {
        var userId = GetCurrentUserId();
        var preset = await _presetService.UpdatePresetAsync(userId, presetId, request);
        return Ok(new { preset.Id, preset.Name, preset.ProviderType, preset.BaseModel });
    }

    [HttpDelete("llm-presets/{presetId}")]
    public async Task<IActionResult> DeleteLLMPreset(Guid presetId)
    {
        var userId = GetCurrentUserId();
        await _presetService.DeletePresetAsync(userId, presetId);
        return Ok(new { message = "Preset deleted." });
    }

    [HttpPost("llm-presets/{presetId}/test")]
    public async Task<IActionResult> TestLLMPreset(Guid presetId)
    {
        var userId = GetCurrentUserId();
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
        var userId = GetCurrentUserId();
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
        var userId = GetCurrentUserId();
        List<LLMInteractionLog> logs;

        if (gameId.HasValue)
        {
            var game = await _context.Games.FindAsync(gameId.Value);
            if (game == null) return NotFound(new { error = "Game not found." });
            if (!HasAccess(game)) return Forbid();
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
        var userId = GetCurrentUserId();
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
        var userId = GetCurrentUserId();
        await _interactionLogger.DeleteLogAsync(userId, logId);
        return Ok(new { message = "Interaction log deleted." });
    }

    [HttpGet("llm-interactions/preset-usage")]
    public async Task<IActionResult> GetPresetUsage(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var userId = GetCurrentUserId();
        var usage = await _interactionLogger.GetPresetUsageAsync(userId, from, to);
        return Ok(usage);
    }

    [HttpPost("llm-interactions/cleanup")]
    public async Task<IActionResult> CleanupOldLogs([FromBody] DateTime before)
    {
        var userId = GetCurrentUserId();
        await _interactionLogger.DeleteOldLogsAsync(userId, before);
        return Ok(new { message = "Old logs cleaned up." });
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

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
            throw new UnauthorizedAccessException();
        return id;
    }

    private bool HasAccess(Game game)
    {
        var userId = GetCurrentUserId();
        return game.CreatorId == userId || game.Players.Any(p => p.UserId == userId && p.Status == PlayerStatus.Active);
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
    public WhisperType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class SendGMWhisperRequest
{
    public List<Guid> TargetPlayerIds { get; set; } = new();
    public WhisperType Type { get; set; }
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
