using System.Security.Cryptography;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Wolverine;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Core message bus for the agentic framework.
/// Routes calls between agents and manages the call lifecycle.
/// </summary>
public interface IAgentBus
{
    /// <summary>
    /// Send a call from one agent to another.
    /// </summary>
    Task<AgentCall> SendCallAsync(AgentCall call);

    /// <summary>
    /// Complete a call with output.
    /// </summary>
    Task<AgentCall> CompleteCallAsync(Guid callId, string? output, string? outputMessage);

    /// <summary>
    /// Fail a call with an error.
    /// </summary>
    Task<AgentCall> FailCallAsync(Guid callId, string error);

    /// <summary>
    /// Get a call by ID.
    /// </summary>
    Task<AgentCall?> GetCallAsync(Guid callId);

    /// <summary>
    /// Get pending calls for a specific agent type.
    /// </summary>
    Task<List<AgentCall>> GetPendingCallsAsync(AgentType toAgent, int limit = 10);

    /// <summary>
    /// Get call history for a game, optionally filtered by agent/action.
    /// </summary>
    Task<List<AgentCall>> GetCallHistoryAsync(Guid gameId, AgentType? fromAgent = null,
        AgentAction? action = null, int limit = 50);

    /// <summary>
    /// Get parent call with all child calls.
    /// </summary>
    Task<(AgentCall? parent, List<AgentCall> children)> GetCallTreeAsync(Guid callId);

    /// <summary>
    /// Execute a call synchronously (for simple agent-to-agent calls).
    /// </summary>
    Task<AgentCall> ExecuteCallAsync(AgentCall call);

    /// <summary>
    /// Activate the GM agent for a game (generates opening narrative).
    /// </summary>
    Task<AgentCall> ActivateGameAgentAsync(Guid gameId, Guid? creatorId);

    /// <summary>
    /// Send a narrative nudge from the Creator to the GM agent.
    /// </summary>
    Task<AgentCall> SendSwayAsync(Guid gameId, Guid creatorId, string direction);

    /// <summary>
    /// Get the current GM agent status for a game.
    /// </summary>
    Task<(GMStatus Status, string? LastAction, DateTime? LastActionAt)> GetGMStatusAsync(Guid gameId);

    /// <summary>
    /// Pause the GM agent for a game.
    /// </summary>
    Task PauseGMAsync(Guid gameId);

    /// <summary>
    /// Resume the GM agent for a game.
    /// </summary>
    Task ResumeGMAsync(Guid gameId);
}

public class AgentBus : IAgentBus
{
    private readonly AppDbContext _context;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly IDiceEngine _diceEngine;
    private readonly SystemRegistry _systemRegistry;
    private readonly ILLMPresetService _presetService;
    private readonly ILLMInteractionLogger _interactionLogger;
    private readonly IGMToolRegistry _toolRegistry;
    private readonly IApiKeyEncryptionService _encryption;
    private readonly ILogger<AgentBus> _logger;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly IMessageContext _messageContext;
    private readonly IDeadLetterQueue _dlq;
    private readonly IConfiguration _configuration;
    private readonly int _toolCallingMaxDepth;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGameAgentManager _gameAgentManager;

    public AgentBus(
        AppDbContext context,
        ILLMProviderFactory providerFactory,
        IGameEngine gameEngine,
        IRAGService ragService,
        IDiceEngine diceEngine,
        SystemRegistry systemRegistry,
        ILLMPresetService presetService,
        ILLMInteractionLogger interactionLogger,
        IGMToolRegistry toolRegistry,
        IApiKeyEncryptionService encryption,
        ILogger<AgentBus> logger,
        IHubContext<GameHub> hubContext,
        IEventBus mediator,
        IMessageContext messageContext,
        IDeadLetterQueue dlq,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IGameAgentManager gameAgentManager)
    {
        _context = context;
        _providerFactory = providerFactory;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _diceEngine = diceEngine;
        _systemRegistry = systemRegistry;
        _presetService = presetService;
        _interactionLogger = interactionLogger;
        _toolRegistry = toolRegistry;
        _encryption = encryption;
        _logger = logger;
        _hubContext = hubContext;
        _gameAgentManager = gameAgentManager;
        _eventBus = mediator;
        _messageContext = messageContext;
        _dlq = dlq;
        _configuration = configuration;
        _toolCallingMaxDepth = _configuration.GetValue<int>("ToolCallingMaxDepth", 5);
        _scopeFactory = scopeFactory;
    }

    private readonly object _decryptionLock = new();

    private ILLMProvider? GetProvider(LLMPreset preset)
    {
        // Decrypt the API key if needed — use lock to prevent concurrent decryption on shared EF entity
        if (preset.ApiKey != null && preset.DecryptedApiKey == null)
        {
            lock (_decryptionLock)
            {
                // Double-check after acquiring lock
                if (preset.DecryptedApiKey != null) return _providerFactory.CreateFromPreset(preset);

                try
                {
                    preset.DecryptedApiKey = _encryption.Decrypt(preset.ApiKey);
                }
                catch (CryptographicException ex)
                {
                    _logger.LogError(ex, "Failed to decrypt API key for preset '{PresetName}'", preset.Name);
                    return null;
                }
            }
        }

        return _providerFactory.CreateFromPreset(preset);
    }

    public async Task<AgentCall> SendCallAsync(AgentCall call)
    {
        call.Status = AgentCallStatus.Pending;
        call.CreatedAt = DateTime.UtcNow;

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[AGENT_CALL] Queued | GameId={GameId} | CallId={CallId} | From={FromAgent} -> To={ToAgent} [{Action}] | SessionId={SessionId}",
            call.GameId, call.Id, call.FromAgent, call.ToAgent, call.Action, call.SessionId);

        // Publish AgentCallQueued event — must go to agent queue (not game queue)
        // to trigger the saga system
        var agentCallQueued = new AgentCallQueued(call.Id, call.GameId);
        await _messageContext.PublishAsync(agentCallQueued);

        return call;
    }

    public async Task<AgentCall> ExecuteCallAsync(AgentCall call)
    {
        // Use scoped context to avoid ObjectDisposedException (AgentBus is a singleton)
        using var scope = _scopeFactory.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        call.Status = AgentCallStatus.Running;
        call.StartedAt = DateTime.UtcNow;
        scopedContext.AgentCalls.Update(call);
        await scopedContext.SaveChangesAsync();

        _logger.LogInformation("[AGENT_CALL] Started | GameId={GameId} | CallId={CallId} | From={FromAgent} -> To={ToAgent} [{Action}]",
            call.GameId, call.Id, call.FromAgent, call.ToAgent, call.Action);

        try
        {
            var result = await DispatchCall(call);

            call.Status = AgentCallStatus.Completed;
            call.Output = result;
            call.CompletedAt = DateTime.UtcNow;
            var callCompletedAt = call.CompletedAt.Value;
            var callStartedAt = call.StartedAt ?? call.CreatedAt;
            call.DurationMs = (int)(callCompletedAt - callStartedAt).TotalMilliseconds;

            scopedContext.AgentCalls.Update(call);
            await scopedContext.SaveChangesAsync();

            // Broadcast narrative output to players for narrative-producing actions
            if ((call.Action == AgentAction.Narrate || call.Action == AgentAction.Generate || call.Action == AgentAction.Nudge || call.Action == AgentAction.OpenNarrative)
                && !string.IsNullOrWhiteSpace(result)
                && !result.StartsWith("{"))
            {
                await BroadcastNarrationAsync(call.GameId, result);
            }

            _logger.LogInformation("[AGENT_CALL] Completed | GameId={GameId} | CallId={CallId} | From={FromAgent} -> To={ToAgent} [{Action}] | Duration={Duration}ms",
                call.GameId, call.Id, call.FromAgent, call.ToAgent, call.Action, call.DurationMs);

            return call;
        }
        catch (Exception ex)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = ex.Message;
            call.CompletedAt = DateTime.UtcNow;
            var failCompletedAt = call.CompletedAt.Value;
            var failStartedAt = call.StartedAt ?? call.CreatedAt;
            call.DurationMs = (int)(failCompletedAt - failStartedAt).TotalMilliseconds;

            scopedContext.AgentCalls.Update(call);
            await scopedContext.SaveChangesAsync();

            _logger.LogError("[AGENT_CALL] Failed | GameId={GameId} | CallId={CallId} | From={FromAgent} -> To={ToAgent} [{Action}] | Duration={Duration}ms | Error={Error}",
                call.GameId, call.Id, call.FromAgent, call.ToAgent, call.Action, call.DurationMs, ex.Message);

            return call;
        }
    }

    public async Task<AgentCall> CompleteCallAsync(Guid callId, string? output, string? outputMessage)
    {
        var call = await _context.AgentCalls.FindAsync(callId);
        if (call == null)
            throw new KeyNotFoundException($"Agent call {callId} not found.");

        call.Status = AgentCallStatus.Completed;
        call.Output = output;
        call.OutputMessage = outputMessage;
        call.CompletedAt = DateTime.UtcNow;
        var completedAt = call.CompletedAt.Value;
        var startedAt = call.StartedAt ?? call.CreatedAt;
        call.DurationMs = (int)(completedAt - startedAt).TotalMilliseconds;

        await _context.SaveChangesAsync();
        return call;
    }

    public async Task<AgentCall> FailCallAsync(Guid callId, string error)
    {
        var call = await _context.AgentCalls.FindAsync(callId);
        if (call == null)
            throw new KeyNotFoundException($"Agent call {callId} not found.");

        call.Status = AgentCallStatus.Failed;
        call.Error = error;
        call.CompletedAt = DateTime.UtcNow;
        var completedAt = call.CompletedAt.Value;
        var startedAt = call.StartedAt ?? call.CreatedAt;
        call.DurationMs = (int)(completedAt - startedAt).TotalMilliseconds;

        await _context.SaveChangesAsync();
        return call;
    }

    public async Task<AgentCall?> GetCallAsync(Guid callId)
    {
        return await _context.AgentCalls
            .Include(a => a.Game)
            .Include(a => a.ParentCall)
            .Include(a => a.ChildCalls)
            .FirstOrDefaultAsync(a => a.Id == callId);
    }

    public async Task<List<AgentCall>> GetPendingCallsAsync(AgentType toAgent, int limit = 10)
    {
        return await _context.AgentCalls
            .Where(a => a.Status == AgentCallStatus.Pending && a.ToAgent == toAgent)
            .OrderBy(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<AgentCall>> GetCallHistoryAsync(Guid gameId, AgentType? fromAgent = null,
        AgentAction? action = null, int limit = 50)
    {
        IQueryable<AgentCall> query = _context.AgentCalls
            .Where(a => a.GameId == gameId)
            .Include(a => a.Game)
            .Include(a => a.ParentCall)
            .OrderByDescending(a => a.CreatedAt);

        if (fromAgent.HasValue)
            query = query.Where(a => a.FromAgent == fromAgent.Value);
        if (action.HasValue)
            query = query.Where(a => a.Action == action.Value);

        return await query.Take(limit).ToListAsync();
    }

    public async Task<(AgentCall? parent, List<AgentCall> children)> GetCallTreeAsync(Guid callId)
    {
        var call = await _context.AgentCalls
            .Include(a => a.ParentCall)
            .Include(a => a.ChildCalls)
            .FirstOrDefaultAsync(a => a.Id == callId);

        var children = call?.ParentCallId == null
            ? await _context.AgentCalls
                .Where(a => a.ParentCallId == callId)
                .ToListAsync()
            : new List<AgentCall>();

        return (call, children);
    }

    /// <summary>
    /// Dispatch a call to the appropriate agent handler.
    /// </summary>
    private async Task<string> DispatchCall(AgentCall call)
    {
        return call.ToAgent switch
        {
            AgentType.LLM => await HandleLLMCall(call),
            AgentType.Dice => HandleDiceCall(call),
            AgentType.RAG => await HandleRAGCall(call),
            AgentType.NPC => await HandleNPCCall(call),
            AgentType.Player => await HandlePlayerCall(call),
            AgentType.System => await HandleSystemCall(call),
            AgentType.GM => await HandleGMCall(call),
            _ => call.Action switch
            {
                AgentAction.CreateCharacter => await HandleCreateCharacter(call),
                _ => throw new ArgumentException($"Unknown agent type: {call.ToAgent}")
            }
        };
    }

    private async Task<string> HandleLLMCall(AgentCall call)
    {
        // Resolve the game's LLM preset to find the correct provider
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == call.GameId);

        string? providerType = null;
        ILLMProvider? provider = null;
        if (game?.LLMPreset != null)
        {
            providerType = game.LLMPreset.ProviderType;
            provider = GetProvider(game.LLMPreset);
        }
        if (provider == null)
        {
            return $"LLM provider not available (tried: {providerType ?? "none"}).";
        }

        try
        {
            string result;

            if (call.Action == AgentAction.Generate || call.Action == AgentAction.Narrate)
            {
                var options = JsonSerializer.Deserialize<LLMDispatchOptions>(call.Input ?? "{}")
                    ?? new LLMDispatchOptions();

                var systemPrompt = options.SystemPrompt ?? "You are a TTRPG Game Master assistant.";
                var userPrompt = options.UserPrompt ?? call.Input ?? "Generate content.";

                // Add language instruction to narration prompts
                if (game != null && !string.IsNullOrEmpty(game.Language) && game.Language != "English")
                {
                    systemPrompt += $"\n\n**Important**: All narrative output must be in **{game.Language}**. Write your response entirely in {game.Language}. Do NOT use English for any narrative content.";
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
                sw.Stop();

                // Log successful interaction
                var model = options?.Options?.Model ?? "default";
                var tokenUsage = provider.GetTokenUsage(result);
                await _interactionLogger.LogInteractionAsync(
                    game.CreatorId, game.LLMPresetId, provider.ProviderId, model,
                    tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                    (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, result,
                    null, null, "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString(),
                    game.LLMPreset?.Name, game.LLMPreset?.EndpointUrl);

                return result;
            }

            if (call.Action == AgentAction.Suggest)
            {
                var options = JsonSerializer.Deserialize<LLMDispatchOptions>(call.Input ?? "{}")
                    ?? new LLMDispatchOptions();

                var systemPrompt = "You are a creative TTRPG Game Master assistant. " +
                    "Provide engaging plot suggestions based on the game context. " +
                    "Respond with a JSON array of suggestions.";

                // Add language instruction for plot suggestions
                if (game != null && !string.IsNullOrEmpty(game.Language) && game.Language != "English")
                {
                    systemPrompt += $"\n\nAll suggestions must be in **{game.Language}**. Write plot titles, descriptions, and all narrative content in {game.Language}.";
                }

                var userPrompt = options.UserPrompt ?? call.Input ?? "Suggest plot continuations.";

                var sw = System.Diagnostics.Stopwatch.StartNew();
                result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
                sw.Stop();

                var model = options?.Options?.Model ?? "default";
                var tokenUsage = provider.GetTokenUsage(result);
                await _interactionLogger.LogInteractionAsync(
                    game.CreatorId, game.LLMPresetId, provider.ProviderId, model,
                    tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                    (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, result,
                    null, null, "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString(),
                    game.LLMPreset?.Name, game.LLMPreset?.EndpointUrl);

                // Try to parse as JSON array
                try
                {
                    var suggestions = JsonSerializer.Deserialize<List<string>>(result);
                    if (suggestions != null)
                        return JsonSerializer.Serialize(suggestions);
                }
                catch
                {
                    // Return raw result if not valid JSON
                }

                return result;
            }

            if (call.Action == AgentAction.Query)
            {
                var options = JsonSerializer.Deserialize<LLMDispatchOptions>(call.Input ?? "{}")
                    ?? new LLMDispatchOptions();

                var systemPrompt = "You are a helpful TTRPG assistant. Answer questions about the game state, rules, and lore.";
                var userPrompt = options.UserPrompt ?? call.Input ?? "Answer the question.";

                var sw = System.Diagnostics.Stopwatch.StartNew();
                result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
                sw.Stop();

                var model = options?.Options?.Model ?? "default";
                var tokenUsage = provider.GetTokenUsage(result);
                await _interactionLogger.LogInteractionAsync(
                    game.CreatorId, game.LLMPresetId, provider.ProviderId, model,
                    tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                    (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, result,
                    null, null, "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString(),
                    game.LLMPreset?.Name, game.LLMPreset?.EndpointUrl);

                return result;
            }

            return "LLM: action not handled.";
        }
        catch (Exception ex)
        {
            // Log failed interaction
            try
            {
                await _interactionLogger.LogFailureAsync(
                    game.CreatorId, game.LLMPresetId, "unknown", "unknown", null, null, null,
                    0, ex.ToString(),
                    "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString(),
                    game.LLMPreset?.Name, game.LLMPreset?.EndpointUrl);
            }
            catch
            {
                // Ignore logging failures
            }

            throw new InvalidOperationException($"LLM call failed: {ex.Message}", ex);
        }
    }

    private string HandleDiceCall(AgentCall call)
    {
        try
        {
            var options = JsonSerializer.Deserialize<DiceDispatchOptions>(call.Input ?? "{}")
                ?? new DiceDispatchOptions();

            var result = _diceEngine.Roll(options.Formula, options.PlayerId);
            var output = new
            {
                formula = result.Formula,
                diceCount = result.DiceCount,
                diceType = result.DiceType,
                modifier = result.Modifier,
                rolls = result.Rolls,
                subtotal = result.Subtotal,
                total = result.Total,
                finalRolls = result.FinalRolls
            };

            return JsonSerializer.Serialize(output);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Dice call failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleRAGCall(AgentCall call)
    {
        try
        {
            var options = JsonSerializer.Deserialize<RAGDispatchOptions>(call.Input ?? "{}")
                ?? new RAGDispatchOptions();

            return call.Action switch
            {
                AgentAction.Check =>
                    JsonSerializer.Serialize(await _ragService.CheckPlotConsistencyAsync(call.GameId, options.MessageCount)),
                AgentAction.Recall =>
                    JsonSerializer.Serialize(await _ragService.FindSimilarPlotThreadsAsync(call.GameId, options.Query ?? "", options.Limit)),
                AgentAction.Generate =>
                    JsonSerializer.Serialize(await _ragService.GeneratePlotContextAsync(call.GameId, options.MessageCount)),
                AgentAction.Suggest =>
                    JsonSerializer.Serialize(await _ragService.SuggestContinuationAsync(call.SessionId ?? Guid.Empty, options.Query ?? "")),
                _ => "RAG: action not handled."
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"RAG call failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleNPCCall(AgentCall call)
    {
        try
        {
            var options = JsonSerializer.Deserialize<NPCDispatchOptions>(call.Input ?? "{}")
                ?? new NPCDispatchOptions();

            // Game-scoped check: ensure NPC belongs to this game
            using var npcScope = _scopeFactory.CreateScope();
            var npcContext = npcScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var npc = await npcContext.NPCs
                .FirstOrDefaultAsync(n => n.Id == options.NpcId && n.GameId == call.GameId);
            if (npc == null)
                return "NPC not found in this game.";

            var output = new
            {
                id = npc.Id,
                name = npc.Name,
                description = npc.Description,
                attributes = npc.Attributes,
                skills = npc.Skills,
                action = call.Action.ToString(),
                result = "NPC action processed."
            };

            return JsonSerializer.Serialize(output);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"NPC call failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandlePlayerCall(AgentCall call)
    {
        try
        {
            var options = JsonSerializer.Deserialize<PlayerDispatchOptions>(call.Input ?? "{}")
                ?? new PlayerDispatchOptions();

            if (call.Action == AgentAction.Query)
            {
                // Game-scoped check: ensure character belongs to this game
                using var playerScope = _scopeFactory.CreateScope();
                var playerContext = playerScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var character = await playerContext.Characters
                    .Include(c => c.Player)
                    .FirstOrDefaultAsync(c => c.Id == options.CharacterId && c.Player!.GameId == call.GameId);
                if (character == null)
                    return "Character not found in this game.";

                var output = new
                {
                    id = character.Id,
                    name = character.Name,
                    characterClass = character.Class,
                    level = character.Level,
                    currentHP = character.CurrentHP,
                    maxHP = character.MaxHP,
                    attributes = character.Attributes,
                    skills = character.Skills,
                    inventory = character.Inventory,
                    spells = character.Spells,
                    conditions = character.Conditions
                };

                return JsonSerializer.Serialize(output);
            }

            return "Player agent: action not handled.";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Player agent call failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleCreateCharacter(AgentCall call)
    {
        try
        {
            var options = JsonSerializer.Deserialize<CharacterCreateOptions>(call.Input ?? "{}")
                ?? new CharacterCreateOptions();

            // Find the player by the call's session/game context
            // The input contains playerId from the hub call
            var playerId = options.PlayerId;
            if (playerId == Guid.Empty)
            {
                return "Player ID required for character creation.";
            }

            using var charScope = _scopeFactory.CreateScope();
            var charContext = charScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await charContext.Players.FindAsync(playerId);
            if (player == null)
                return "Player not found.";

            var character = new Character
            {
                PlayerId = player.Id,
                Name = options.Name,
                Class = options.Class,
                Level = options.Level ?? 1,
                ProficiencyBonus = GetProficiencyBonus(options.Level ?? 1),
                CurrentHP = options.CurrentHP ?? 10,
                MaxHP = options.MaxHP ?? 10,
                Attributes = JsonSerializer.SerializeToElement(options.Attributes),
                Skills = JsonSerializer.SerializeToElement(options.Skills),
                Inventory = JsonSerializer.SerializeToElement(options.Inventory),
                Spells = JsonSerializer.SerializeToElement(new { }),
                Conditions = JsonSerializer.SerializeToElement(new { }),
                CustomFields = JsonSerializer.SerializeToElement(new { systemId = options.SystemId }),
                UpdatedAt = DateTime.UtcNow
            };

            charContext.Characters.Add(character);
            await charContext.SaveChangesAsync();

            return $"Character created: {character.Id}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Character creation failed: {ex.Message}", ex);
        }
    }

    private static int GetProficiencyBonus(int level)
    {
        if (level <= 4) return 2;
        if (level <= 8) return 3;
        if (level <= 12) return 4;
        if (level <= 16) return 5;
        return 6;
    }

    private async Task<string> HandleSystemCall(AgentCall call)
    {
        try
        {
            var options = JsonSerializer.Deserialize<SystemDispatchOptions>(call.Input ?? "{}")
                ?? new SystemDispatchOptions();

            var system = GetSystemForCall(call, options.SystemId);
            if (system == null)
                return $"System '{options.SystemId}' not found.";

            var output = new
            {
                systemId = system.Id,
                name = system.Name,
                version = system.Version,
                action = call.Action.ToString(),
                result = "System rules applied."
            };

            return JsonSerializer.Serialize(output);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"System call failed: {ex.Message}", ex);
        }
    }

    private async Task<string> HandleGMCall(AgentCall call)
    {
        // GM agent is the orchestrator - it manages state and delegates
        var options = JsonSerializer.Deserialize<GMDispatchOptions>(call.Input ?? "{}")
            ?? new GMDispatchOptions();

        // Use scoped context to avoid ObjectDisposedException
        using var gmScope = _scopeFactory.CreateScope();
        var gmContext = gmScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await gmContext.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == call.GameId);
        if (game == null)
            return "Game not found.";

        if (call.Action == AgentAction.ManageState)
        {
            game.GameState = options.StateJson ?? game.GameState;
            game.LastGMAction = "ManageState";
            game.LastGMActionAt = DateTime.UtcNow;
            await gmContext.SaveChangesAsync();
            await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
            {
                GameId = game.Id,
                Status = game.GMStatus,
                LastAction = game.LastGMAction,
                ChangedAt = game.LastGMActionAt
            });
            return "Game state updated.";
        }

        if (call.Action == AgentAction.Narrate || call.Action == AgentAction.Generate)
        {
            // Use the game's LLM preset to generate narrative with tool calling
            if (game.LLMPreset == null)
                return "No LLM preset configured for this game.";

            var provider = GetProvider(game.LLMPreset);
            if (provider == null)
                return $"LLM provider '{game.LLMPreset.ProviderType}' not available.";

            var systemPrompt = options.SystemPrompt ??
                $"You are the Game Master for a TTRPG session. " +
                $"Game system: {game.SystemId}. " +
                $"Plot seed: {game.PlotSeed ?? "None"}. " +
                $"Game parameters: {game.GameParameters ?? "None"}. " +
                $"Current game state: {game.GameState ?? "None"}. " +
                $"You have access to game tools (dice rolls, skill checks, player queries). " +
                $"Use them when appropriate to enhance the game experience.";

            // Add language instruction to the default system prompt
            if (!string.IsNullOrEmpty(game.Language) && game.Language != "English")
            {
                systemPrompt += $"\n\n**Language**: All narrative output must be in **{game.Language}**. Write your response entirely in {game.Language}. Do NOT use English for any narrative content. (NPCs speaking in their native unknown language may be described in English for player comprehension.)";
            }

            var userPrompt = options.UserPrompt ?? call.Input ?? "Continue the narrative.";

            // Get available tools for this game
            var tools = _toolRegistry.GetAvailableTools(game.Id);

            // Call LLM with tool calling support
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var completion = await provider.CompleteWithToolsAsync(systemPrompt, userPrompt, tools, options.Options);
            sw.Stop();

            // Log successful interaction
            var model = options?.Options?.Model ?? game.LLMPreset.BaseModel;
            var tokenUsage = completion.TokenUsage ?? provider.GetTokenUsage(completion.Content);
            await _interactionLogger.LogInteractionAsync(
                game.CreatorId, game.LLMPresetId, provider.ProviderId, model,
                tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, completion.Content,
                null, provider.EndpointUrl, "agent", call.GameId, call.SessionId,
                "GM", call.Action.ToString(),
                game.LLMPreset?.Name, game.LLMPreset?.EndpointUrl);

            // Handle tool calls from the LLM
            if (completion.HasToolCalls)
            {
                _logger.LogInformation("[TOOL_CALL] LLMRequested | GameId={GameId} | CallId={CallId} | ToolCount={Count} | Tools={Tools}",
                    game.Id, call.Id, completion.ToolCalls.Count,
                    string.Join(", ", completion.ToolCalls.Select(tc => tc.Name)));

                var toolResults = new List<(string toolCallId, string result, string message)>();

                foreach (var toolCall in completion.ToolCalls)
                {
                    var toolResult = await ExecuteToolCallAsync(game.Id, call.SessionId ?? Guid.Empty, toolCall);
                    toolResults.Add((toolCall.Id, toolResult.Output ?? "", toolResult.OutputMessage ?? ""));

                    // If tool requires confirmation, save and return early
                    if (toolResult.RequiresUserInput)
                    {
                        game.LastGMAction = $"ToolCall: {toolCall.Name} (waiting confirmation)";
                        game.LastGMActionAt = DateTime.UtcNow;
                        await gmContext.SaveChangesAsync();

                        await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
                        {
                            GameId = game.Id,
                            Status = game.GMStatus,
                            LastAction = game.LastGMAction,
                            ChangedAt = game.LastGMActionAt
                        });

                        // Return tool call info for frontend notification
                        return JsonSerializer.Serialize(new { toolCallId = toolCall.Id, toolName = toolCall.Name, waitingConfirmation = true });
                    }
                }

                // Feed tool results back to LLM for final narrative
                var toolResultsText = string.Join("\n", toolResults.Select(tr =>
                    $"Tool '{tr.toolCallId}': {tr.message}\nResult: {tr.result}"));

                var followUpPrompt = $"Tool results:\n{toolResultsText}\n\nNow continue the narrative based on these results.";

                _logger.LogInformation("[TOOL_CALL] FollowUpLLM | GameId={GameId} | CallId={CallId} | ToolCalls={Count} | PromptPreview={Preview}",
                    game.Id, call.Id, toolResults.Count,
                    followUpPrompt.Length > 100 ? followUpPrompt[..100] + "..." : followUpPrompt);

                var followUpCompletion = await provider.CompleteAsync(
                    systemPrompt, followUpPrompt, options.Options);

                // If follow-up returned empty content, extract narrative from tool call context
                if (string.IsNullOrWhiteSpace(followUpCompletion))
                {
                    var narrativeContext = toolResults
                        .Where(tr => tr.result.Contains("\"context\""))
                        .Select(tr =>
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(tr.result, @"""context""\s*:\s*""(.*?)""" , System.Text.RegularExpressions.RegexOptions.Singleline);
                            return m.Success && m.Groups[1].Value.Length > 50 ? m.Groups[1].Value : null;
                        })
                        .FirstOrDefault(c => c != null);

                    if (!string.IsNullOrWhiteSpace(narrativeContext))
                    {
                        _logger.LogInformation("[TOOL_CALL] EmptyFollowUpExtracted | GameId={GameId} | ExtractedNarrativeLen={Len}",
                            game.Id, narrativeContext.Length);
                        followUpCompletion = narrativeContext;
                    }
                }

                game.LastGMAction = $"Narrate (with {completion.ToolCalls.Count} tool calls)";
                game.LastGMActionAt = DateTime.UtcNow;
                await gmContext.SaveChangesAsync();

                await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
                {
                    GameId = game.Id,
                    Status = game.GMStatus,
                    LastAction = game.LastGMAction,
                    ChangedAt = game.LastGMActionAt
                });

                return followUpCompletion;
            }

            // Check if content looks like a JSON array of tool calls (LLM returned tool calls as text)
            string? narrativeContent = completion.Content;
            var trimmedContent = completion.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedContent) && trimmedContent.StartsWith("["))
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(trimmedContent);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var tc in doc.RootElement.EnumerateArray())
                        {
                            if (tc.TryGetProperty("name", out var nameProp) && nameProp.GetString() == "narrate")
                            {
                                if (tc.TryGetProperty("arguments", out var argsProp) && argsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (argsProp.TryGetProperty("context", out var contextProp))
                                    {
                                        var narrativeContext = contextProp.GetString();
                                        if (!string.IsNullOrWhiteSpace(narrativeContext) && narrativeContext.Length > 50)
                                        {
                                            narrativeContent = narrativeContext;
                                            _logger.LogInformation("[TOOL_CALL] TextNarrateExtracted | GameId={GameId} | ExtractedNarrativeLen={Len}",
                                                game.Id, narrativeContext.Length);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Not a valid JSON array — use as-is
                }
            }

            game.LastGMAction = "Narrate";
            game.LastGMActionAt = DateTime.UtcNow;
            await gmContext.SaveChangesAsync();

            await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
            {
                GameId = game.Id,
                Status = game.GMStatus,
                LastAction = game.LastGMAction,
                ChangedAt = game.LastGMActionAt
            });

            return narrativeContent;
        }

        if (call.Action == AgentAction.Nudge)
        {
            // Incorporate Creator's narrative direction into the story
            if (game.LLMPreset == null)
                return "No LLM preset configured for this game.";

            var provider = GetProvider(game.LLMPreset);
            if (provider == null)
                return $"LLM provider '{game.LLMPreset.ProviderType}' not available.";

            var systemPrompt = $"You are the Game Master for a TTRPG session. " +
                $"The game creator has sent a narrative nudge: {call.Input}. " +
                $"Incorporate this direction naturally into the ongoing story. " +
                $"Game system: {game.SystemId}. " +
                $"Current game state: {game.GameState ?? "None"}. " +
                $"Respond with an immersive narrative that follows the creator's direction.";

            // Add language instruction to the nudge prompt
            if (!string.IsNullOrEmpty(game.Language) && game.Language != "English")
            {
                systemPrompt += $"\n\n**Language**: All narrative output must be in **{game.Language}**. Write your response entirely in {game.Language}.";
            }

            var userPrompt = $"Incorporate this narrative direction: {call.Input}";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
            sw.Stop();

            // Log successful interaction
            var model = options?.Options?.Model ?? game.LLMPreset.BaseModel;
            var tokenUsage = provider.GetTokenUsage(result);
            await _interactionLogger.LogInteractionAsync(
                game.CreatorId, game.LLMPresetId, provider.ProviderId, model,
                tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, result,
                null, provider.EndpointUrl, "agent", call.GameId, call.SessionId,
                "GM", call.Action.ToString(),
                game.LLMPreset?.Name, game.LLMPreset?.EndpointUrl);

            game.LastGMAction = "Nudge";
            game.LastGMActionAt = DateTime.UtcNow;
            await gmContext.SaveChangesAsync();

            await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
            {
                GameId = game.Id,
                Status = game.GMStatus,
                LastAction = game.LastGMAction,
                ChangedAt = game.LastGMActionAt
            });

            return result;
        }

        if (call.Action == AgentAction.Notify)
        {
            return "Notification sent to relevant agents.";
        }

        if (call.Action == AgentAction.GenerateInitialThreads)
        {
            return await HandleGenerateInitialThreads(call.GameId, options);
        }

        if (call.Action == AgentAction.OpenNarrative)
        {
            return await HandleOpenNarrative(call.GameId, options);
        }

        return "GM agent: action not handled.";
    }

    /// <summary>
    /// Execute a single tool call requested by the GM LLM.
    /// Returns the result, or flags it as requiring user confirmation.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteToolCallAsync(Guid gameId, Guid sessionId, ToolCall toolCall)
    {
        var toolName = toolCall.Name;
        var args = toolCall.Arguments;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Use scoped context to avoid ObjectDisposedException
        using var toolScope = _scopeFactory.CreateScope();
        var toolContext = toolScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Lazy cleanup: remove expired tool calls waiting confirmation
        try
        {
            var expired = await toolContext.GMToolCalls
                .Where(tc => tc.Status == ToolCallStatus.WaitingConfirmation &&
                            tc.CreatedAt + tc.ExpirationTime < DateTime.UtcNow)
                .ToListAsync();
            foreach (var tc in expired)
            {
                tc.Status = ToolCallStatus.Cancelled;
                tc.Error = "Expired: waiting for confirmation timed out";
                tc.CompletedAt = DateTime.UtcNow;
            }
            if (expired.Any())
            {
                await toolContext.SaveChangesAsync();
                _logger.LogDebug("[TOOL_CALL] ExpiredCleanup | CleanedUp={Count}", expired.Count);
            }
        }
        catch { /* Ignore cleanup failures */ }

        // Log the tool call
        var toolCallRecord = new GMToolCall
        {
            GameId = gameId,
            SessionId = sessionId,
            ToolName = toolName,
            ToolCallId = toolCall.Id,
            Arguments = args,
            Status = ToolCallStatus.Executing,
            CreatedAt = DateTime.UtcNow
        };
        toolContext.GMToolCalls.Add(toolCallRecord);
        await toolContext.SaveChangesAsync();

        var argsPreview = args?.Length > 200 ? args[..200] + "..." : (args ?? "{}");
        _logger.LogInformation("[TOOL_CALL] Executing | GameId={GameId} | ToolCallId={ToolCallId} | Tool={Tool} | Args={Args}",
            gameId, toolCall.Id, toolName, argsPreview);

        // Execute the tool
        var result = await _toolRegistry.ExecuteToolAsync(gameId, sessionId, toolName, args);
        sw.Stop();

        var outputPreview = result.Output?.Length > 200 ? result.Output[..200] + "..." : result.Output;
        _logger.LogInformation("[TOOL_CALL] Executed | GameId={GameId} | ToolCallId={ToolCallId} | Tool={Tool} | Success={Success} | Duration={Duration}ms | Output={Output} | RequiresConfirmation={RequiresConfirmation}",
            gameId, toolCall.Id, toolName, result.Success, sw.ElapsedMilliseconds, outputPreview, result.RequiresUserInput);

        toolCallRecord.Status = result.Success ? ToolCallStatus.Completed : ToolCallStatus.Failed;
        toolCallRecord.Result = result.Output;
        toolCallRecord.OutputMessage = result.OutputMessage;
        toolCallRecord.Error = result.Error;
        toolCallRecord.CompletedAt = DateTime.UtcNow;
        await toolContext.SaveChangesAsync();

        // If tool requires user confirmation, flag it
        if (_toolRegistry.RequiresConfirmation(toolName) && result.Success)
        {
            toolCallRecord.Status = ToolCallStatus.WaitingConfirmation;
            await toolContext.SaveChangesAsync();

            _logger.LogInformation("[TOOL_CALL] WaitingConfirmation | GameId={GameId} | ToolCallId={ToolCallId} | Tool={Tool} | InputType={InputType}",
                gameId, toolCall.Id, toolName, result.UserInputType);

            return new ToolExecutionResult
            {
                Success = true,
                Output = result.Output,
                OutputMessage = result.OutputMessage,
                RequiresUserInput = true,
                UserInputType = result.UserInputType,
            };
        }

        return result;
    }

    /// <summary>
    /// Handle GenerateInitialThreads action — generates initial plot threads via LLM.
    /// </summary>
    private async Task<string> HandleGenerateInitialThreads(Guid gameId, GMDispatchOptions options)
    {
        // Use scoped context to load game data
        using var gameScope = _scopeFactory.CreateScope();
        var gameContext = gameScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await gameContext.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            return $"Game {gameId} not found.";

        if (game.LLMPreset == null)
            return "No LLM preset configured for this game.";

        var provider = GetProvider(game.LLMPreset);
        if (provider == null)
            return $"LLM provider '{game.LLMPreset.ProviderType}' not available.";

        var systemPrompt = options.SystemPrompt ?? $"You are the Game Master for a TTRPG session. " +
            $"Generate initial plot threads for this game. " +
            $"Plot seed: {game.PlotSeed ?? "No premise provided."}. " +
            $"Game parameters: {game.GameParameters ?? "Standard tone and difficulty."}. " +
            $"Game system: {game.SystemId}. " +
            $"Each thread must have: title (string), category (Personal, Threat, Faction, Mystery, or Adventure), " +
            $"description (string), nextMilestone (string), foreshadowing (string). " +
            $"Generate 2-4 threads appropriate for the premise.";
        var userPrompt = options.UserPrompt ?? "Generate initial plot threads for this game.";

        // Build a JSON schema for the expected output
        var threadsSchemaJson = "{\"type\":\"array\",\"items\":{\"$ref\":\"#/definitions/PlotThread\"},\"definitions\":{\"PlotThread\":{\"type\":\"object\",\"properties\":{\"title\":{\"type\":\"string\"},\"category\":{\"type\":\"string\",\"enum\":[\"Personal\",\"Threat\",\"Faction\",\"Mystery\",\"Adventure\"]},\"description\":{\"type\":\"string\"},\"nextMilestone\":{\"type\":\"string\"},\"foreshadowing\":{\"type\":\"string\"}},\"required\":[\"title\",\"category\",\"description\",\"nextMilestone\",\"foreshadowing\"]}}}";

        var threadsSchema = new JsonSchemaOutput
        {
            Name = "PlotThreadsArray",
            Schema = JsonDocument.Parse(threadsSchemaJson).RootElement
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var jsonOptions = options.Options ?? new LLMOptions();
        var jsonResult = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
        {
            Model = jsonOptions.Model,
            Temperature = jsonOptions.Temperature,
            MaxTokens = jsonOptions.MaxTokens,
            TopP = jsonOptions.TopP,
            JsonSchemaOutput = threadsSchema
        });
        sw.Stop();

        // Parse and save the generated threads — use robust JSON extraction
        var extractedJson = JsonExtract.Extract(jsonResult);
        if (extractedJson == null)
        {
            // Retry with a simpler prompt (some models struggle with JSON schema output)
            _logger.LogWarning("[PLOTWEAVER] NoExtractableJson | GameId={GameId} | RawResponseLen={Len} | Retrying with simpler prompt",
                game.Id, jsonResult?.Length ?? 0);

            var simpleSystemPrompt = $"You are the Game Master for a TTRPG session. " +
                $"Generate initial plot threads for this game. " +
                $"Plot seed: {game.PlotSeed ?? "No premise provided."}. " +
                $"Game system: {game.SystemId}. " +
                $"Return a JSON array of 2-4 plot threads. Each thread has: title, category (Personal|Threat|Faction|Mystery|Adventure), description, nextMilestone, foreshadowing.";

            var retryResult = await provider.CompleteAsync(
                simpleSystemPrompt,
                "Generate initial plot threads as a JSON array. Return ONLY the JSON array, nothing else.",
                new LLMOptions
                {
                    Model = jsonOptions.Model,
                    Temperature = Math.Max(0.3f, jsonOptions.Temperature - 0.3f), // Lower temperature for more deterministic output
                    MaxTokens = jsonOptions.MaxTokens,
                    TopP = jsonOptions.TopP
                    // No JsonSchemaOutput — let JsonExtract.Extract handle parsing
                });

            extractedJson = JsonExtract.Extract(retryResult);
            if (extractedJson == null)
            {
                _logger.LogWarning("[PLOTWEAVER] RetryFailed | GameId={GameId} | RawResponse={Raw}",
                    game.Id, retryResult?.Length > 200 ? retryResult[..200] + "..." : retryResult);

                // Fallback: generate default plot threads from the plot seed
                return await GenerateFallbackPlotThreads(game, jsonOptions);
            }
        }

        try
        {
            var threads = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(extractedJson);
            if (threads != null && threads.Any())
            {
                var newThreads = new List<PlotThread>();
                foreach (var threadData in threads)
                {
                    var thread = new PlotThread
                    {
                        GameId = game.Id,
                        Title = threadData.GetValueOrDefault("title")?.ToString() ?? "Untitled Thread",
                        Category = Enum.TryParse<PlotThreadCategory>(threadData.GetValueOrDefault("category")?.ToString(), true, out var cat) 
                            ? cat : PlotThreadCategory.Personal,
                        Description = threadData.GetValueOrDefault("description")?.ToString() ?? "",
                        NextMilestone = threadData.GetValueOrDefault("nextMilestone")?.ToString(),
                        Foreshadowing = threadData.GetValueOrDefault("foreshadowing")?.ToString(),
                        Momentum = 0f,
                        RelevanceScore = 0.5f,
                        Status = PlotThreadStatus.Active
                    };
                    newThreads.Add(thread);
                }

                // Use scoped context to avoid ObjectDisposedException
                using var scope = _scopeFactory.CreateScope();
                var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                scopedContext.PlotThreads.AddRange(newThreads);
                await scopedContext.SaveChangesAsync();

                _logger.LogInformation("[PLOTWEAVER] GeneratedInitialThreads | GameId={GameId} | Count={Count} | Duration={Duration}ms",
                    game.Id, newThreads.Count, sw.ElapsedMilliseconds);

                return JsonSerializer.Serialize(new { threadCount = newThreads.Count, threads = newThreads.Select(t => new { t.Id, t.Title, t.Category }) });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PLOTWEAVER] FailedToParseInitialThreads | GameId={GameId}", game.Id);
        }

        return "Failed to generate initial threads.";
    }

    /// <summary>
    /// Generate fallback plot threads from the game's plot seed when LLM fails.
    /// </summary>
    private async Task<string> GenerateFallbackPlotThreads(Game game, LLMOptions? options)
    {
        _logger.LogInformation("[PLOTWEAVER] GeneratingFallbackThreads | GameId={GameId} | PlotSeed={Seed}",
            game.Id, game.PlotSeed);

        // Use LLM with very simple prompt to generate fallback threads
        var provider = GetProvider(game.LLMPreset!);
        if (provider == null)
        {
            // Ultimate fallback: create generic threads from plot seed
            var fallbackThreads = new List<PlotThread>
            {
                new() { GameId = game.Id, Title = "The Mystery Unfolds", Category = PlotThreadCategory.Mystery, Description = "Discover the secrets hidden beneath the school.", NextMilestone = "Find the first clue", Foreshadowing = "Whispers of ancient treasures", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active },
                new() { GameId = game.Id, Title = "The Teachers' Secret", Category = PlotThreadCategory.Threat, Description = "The teachers are hiding something in the basement.", NextMilestone = "Confront a teacher", Foreshadowing = "Nervous glances toward the basement", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active },
                new() { GameId = game.Id, Title = "The Hidden Entrance", Category = PlotThreadCategory.General, Description = "Find the secret entrance to the basement.", NextMilestone = "Locate the entrance", Foreshadowing = "The iron key left on the desk", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active }
            };

            using var scope = _scopeFactory.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            scopedContext.PlotThreads.AddRange(fallbackThreads);
            await scopedContext.SaveChangesAsync();

            return JsonSerializer.Serialize(new { threadCount = fallbackThreads.Count, threads = fallbackThreads.Select(t => new { t.Id, t.Title, t.Category }), fallback = true });
        }

        var systemPrompt = $"You are a creative TTRPG Game Master. Generate 3 plot threads for a game with this premise: {game.PlotSeed ?? "No premise"}. Return ONLY a JSON array. Each thread: title, category (Personal|Threat|Faction|Mystery|Adventure), description, nextMilestone, foreshadowing.";

        var retryResult = await provider.CompleteAsync(
            systemPrompt,
            "Return a JSON array of 3 plot threads.",
            new LLMOptions
            {
                Model = options?.Model,
                Temperature = 0.3f,
                MaxTokens = options?.MaxTokens ?? 1024,
                TopP = options != null && options.TopP > 0 ? options.TopP : 0.9f,
                JsonSchemaOutput = new JsonSchemaOutput
                {
                    Name = "PlotThreadsArray",
                    Schema = JsonDocument.Parse(@"{""type"":""array"",""items"":{""$ref"":""#/definitions/PlotThread""},""definitions"":{""PlotThread"":{""type"":""object"",""properties"":{""title"":{""type"":""string""},""category"":{""type"":""string"",""enum"":[""Personal"",""Threat"",""Faction"",""Mystery"",""Adventure""]},""description"":{""type"":""string""},""nextMilestone"":{""type"":""string""},""foreshadowing"":{""type"":""string""}},""required"":[""title"",""category"",""description"",""nextMilestone"",""foreshadowing""]}}}}").RootElement
                }
            });

        var extractedJson = JsonExtract.Extract(retryResult);
        if (extractedJson == null)
        {
            _logger.LogWarning("[PLOTWEAVER] UltimateFallback | GameId={GameId}", game.Id);
            return await GenerateFallbackPlotThreadsFromSeed(game);
        }

        try
        {
            var threads = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(extractedJson);
            if (threads != null && threads.Any())
            {
                var newThreads = new List<PlotThread>();
                foreach (var threadData in threads)
                {
                    var thread = new PlotThread
                    {
                        GameId = game.Id,
                        Title = threadData.GetValueOrDefault("title")?.ToString() ?? "Untitled Thread",
                        Category = Enum.TryParse<PlotThreadCategory>(threadData.GetValueOrDefault("category")?.ToString(), true, out var cat) ? cat : PlotThreadCategory.Personal,
                        Description = threadData.GetValueOrDefault("description")?.ToString() ?? "",
                        NextMilestone = threadData.GetValueOrDefault("nextMilestone")?.ToString(),
                        Foreshadowing = threadData.GetValueOrDefault("foreshadowing")?.ToString(),
                        Momentum = 0f,
                        RelevanceScore = 0.5f,
                        Status = PlotThreadStatus.Active
                    };
                    newThreads.Add(thread);
                }

                using var scope = _scopeFactory.CreateScope();
                var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                scopedContext.PlotThreads.AddRange(newThreads);
                await scopedContext.SaveChangesAsync();

                return JsonSerializer.Serialize(new { threadCount = newThreads.Count, threads = newThreads.Select(t => new { t.Id, t.Title, t.Category }), fallback = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PLOTWEAVER] FallbackParseFailed | GameId={GameId}", game.Id);
        }

        return await GenerateFallbackPlotThreadsFromSeed(game);
    }

    private async Task<string> GenerateFallbackPlotThreadsFromSeed(Game game)
    {
        var fallbackThreads = new List<PlotThread>
        {
            new() { GameId = game.Id, Title = "The Mystery Unfolds", Category = PlotThreadCategory.Mystery, Description = "Discover the secrets hidden in the game world.", NextMilestone = "Find the first clue", Foreshadowing = "Whispers of something hidden", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active },
            new() { GameId = game.Id, Title = "The Hidden Threat", Category = PlotThreadCategory.Threat, Description = "A hidden danger lurks beneath the surface.", NextMilestone = "Discover the threat", Foreshadowing = "Signs of something wrong", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active },
            new() { GameId = game.Id, Title = "The Adventure Begins", Category = PlotThreadCategory.General, Description = "A new adventure awaits in the unknown.", NextMilestone = "Take the first step", Foreshadowing = "An invitation to explore", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active }
        };

        using var scope = _scopeFactory.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scopedContext.PlotThreads.AddRange(fallbackThreads);
        await scopedContext.SaveChangesAsync();

        return JsonSerializer.Serialize(new { threadCount = fallbackThreads.Count, threads = fallbackThreads.Select(t => new { t.Id, t.Title, t.Category }), fallback = true });
    }

    /// <summary>
    /// Handle OpenNarrative action — generates opening narrative with iterative tool calling.
    /// </summary>
    private async Task<string> HandleOpenNarrative(Guid gameId, GMDispatchOptions options)
    {
        // Use scoped context to load game data
        using var gameScope = _scopeFactory.CreateScope();
        var gameContext = gameScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await gameContext.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            return $"Game {gameId} not found.";

        if (game.LLMPreset == null)
            return "No LLM preset configured for this game.";

        var provider = GetProvider(game.LLMPreset);
        if (provider == null)
            return $"LLM provider '{game.LLMPreset.ProviderType}' not available.";

        var systemPrompt = options.SystemPrompt ??
            $"You are the Game Master for a TTRPG session. " +
            $"Create an immersive opening narrative that introduces the world, sets the tone, " +
            $"and invites the players into the story. Be vivid and engaging. " +
            $"Game system: {game.SystemId}. " +
            $"Plot seed: {game.PlotSeed ?? "No premise provided."}. " +
            $"Game parameters: {game.GameParameters ?? "Standard tone and difficulty."}. " +
            $"You have access to game tools. Use the 'narrate' tool to generate the opening scene.";

        // Add language instruction to the default system prompt
        if (!string.IsNullOrEmpty(game.Language) && game.Language != "English")
        {
            systemPrompt += $"\n\n**Language**: All narrative output must be in **{game.Language}**. Write your response entirely in {game.Language}. Do NOT use English for any narrative content. (NPCs speaking in their native unknown language may be described in English for player comprehension.)";
        }

        var userPrompt = options.UserPrompt ?? "Generate the opening narrative for this game session.";
        var tools = _toolRegistry.GetAvailableTools(game.Id);

        // Iterative tool calling loop
        var currentSystemPrompt = systemPrompt;
        var currentFollowUpPrompt = userPrompt;
        int currentDepth = 0;
        string? lastNarrative = null;
        var allToolCalls = new List<ToolCall>();
        var toolResults = new List<(string toolCallId, string result, string message)>();

        _logger.LogInformation("[OPEN_NARRATIVE] Starting iterative tool calling for game {GameId} | MaxDepth={MaxDepth}",
            game.Id, _toolCallingMaxDepth);

        while (currentDepth < _toolCallingMaxDepth)
        {
            currentDepth++;
            _logger.LogInformation("[OPEN_NARRATIVE] Iteration={Depth} | GameId={GameId} | PromptLen={PromptLen}",
                currentDepth, game.Id, currentFollowUpPrompt.Length);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var completion = await provider.CompleteWithToolsAsync(currentSystemPrompt, currentFollowUpPrompt, tools, options.Options);
            sw.Stop();

            // Log successful interaction
            var model = options?.Options?.Model ?? game.LLMPreset.BaseModel;
            var tokenUsage = completion.TokenUsage ?? provider.GetTokenUsage(completion.Content);
            await _interactionLogger.LogInteractionAsync(
                game.CreatorId, game.LLMPresetId, provider.ProviderId, model,
                tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                (int)sw.ElapsedMilliseconds, currentSystemPrompt, currentFollowUpPrompt, completion.Content,
                null, provider.EndpointUrl, "agent", game.Id, null,
                "GM", "OpenNarrative",
                game.LLMPreset?.Name, game.LLMPreset?.EndpointUrl);

            if (!completion.HasToolCalls)
            {
                // Check if the content looks like a JSON array of tool calls (LLM returned tool calls as text)
                string? extractedNarrative = null;
                var trimmedContent = completion.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedContent) && trimmedContent.StartsWith("["))
                {
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(trimmedContent);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var tc in doc.RootElement.EnumerateArray())
                            {
                                if (tc.TryGetProperty("name", out var nameProp) && nameProp.GetString() == "narrate")
                                {
                                    if (tc.TryGetProperty("arguments", out var argsProp) && argsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                                    {
                                        if (argsProp.TryGetProperty("context", out var contextProp))
                                        {
                                            var narrativeContext = contextProp.GetString();
                                            if (!string.IsNullOrWhiteSpace(narrativeContext) && narrativeContext.Length > 50)
                                            {
                                                extractedNarrative = narrativeContext;
                                                _logger.LogInformation("[OPEN_NARRATIVE] TextToolCallExtracted | GameId={GameId} | Depth={Depth} | ExtractedNarrativeLen={Len}",
                                                    game.Id, currentDepth, narrativeContext.Length);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Not a valid JSON array — treat as regular content
                    }
                }

                if (extractedNarrative != null)
                {
                    lastNarrative = extractedNarrative;
                }
                else
                {
                    lastNarrative = completion.Content;
                }
                allToolCalls.AddRange(completion.ToolCalls);
                _logger.LogInformation("[OPEN_NARRATIVE] FinalNarrative | GameId={GameId} | Depth={Depth} | TotalToolCalls={TotalCalls} | NarrativeLen={NarrativeLen}",
                    game.Id, currentDepth, allToolCalls.Count, lastNarrative?.Length ?? 0);
                break;
            }

            // Check if the LLM used the 'narrate' tool as its FINAL tool call
            // In that case, the narrative text is in the tool call's 'context' argument
            var narrateToolCall = completion.ToolCalls.FirstOrDefault(tc => tc.Name == "narrate");
            if (narrateToolCall != null && currentFollowUpPrompt == userPrompt)
            {
                // This is the first iteration and LLM used 'narrate' directly
                // Extract narrative from the tool call's context argument
                try
                {
                    var args = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(narrateToolCall.Arguments ?? "{}") ?? new();
                    var narrativeContext = args.GetValueOrDefault("context")?.ToString();
                    if (!string.IsNullOrWhiteSpace(narrativeContext) && narrativeContext.Length > 50)
                    {
                        lastNarrative = narrativeContext;
                        allToolCalls.AddRange(completion.ToolCalls);
                        _logger.LogInformation("[OPEN_NARRATIVE] DirectNarrateTool | GameId={GameId} | Depth={Depth} | ExtractedNarrativeLen={Len}",
                            game.Id, currentDepth, narrativeContext.Length);
                        break;
                    }
                }
                catch
                {
                    // JSON parse failed — continue with normal flow
                }
            }

            _logger.LogInformation("[OPEN_NARRATIVE] ToolCallsFound | GameId={GameId} | Depth={Depth} | Count={Count} | Tools={Tools}",
                game.Id, currentDepth, completion.ToolCalls.Count,
                string.Join(", ", completion.ToolCalls.Select(tc => tc.Name)));

            allToolCalls.AddRange(completion.ToolCalls);
            toolResults.Clear();

            // Execute tool calls
            foreach (var toolCall in completion.ToolCalls)
            {
                var toolResult = await ExecuteToolCallAsync(game.Id, Guid.Empty, toolCall);
                toolResults.Add((toolCall.Id, toolResult.Output ?? "", toolResult.OutputMessage ?? ""));

                // If tool requires confirmation, save and return early
                if (toolResult.RequiresUserInput)
                {
                    // Use scoped context to avoid ObjectDisposedException
                    using var scope = _scopeFactory.CreateScope();
                    var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var scopedGame = await scopedContext.Games.FindAsync(game.Id);
                    if (scopedGame != null)
                    {
                        scopedGame.LastGMAction = $"ToolCall: {toolCall.Name} (waiting confirmation)";
                        scopedGame.LastGMActionAt = DateTime.UtcNow;
                        await scopedContext.SaveChangesAsync();
                    }

                    await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
                    {
                        GameId = game.Id,
                        Status = game.GMStatus,
                        LastAction = $"ToolCall: {toolCall.Name} (waiting confirmation)",
                        ChangedAt = DateTime.UtcNow
                    });

                    // Return tool call info for frontend notification
                    return JsonSerializer.Serialize(new { toolCallId = toolCall.Id, toolName = toolCall.Name, waitingConfirmation = true });
                }
            }

            // Feed results back with tools re-sent
            var toolResultsText = string.Join("\n", toolResults.Select(tr =>
                $"Tool '{tr.toolCallId}': {tr.message}\nResult: {tr.result}"));

            currentFollowUpPrompt = $"Tool results:\n{toolResultsText}\n\nNow continue the narrative based on these results.";
            currentSystemPrompt = systemPrompt; // Reset system prompt each round
        }

        // Max depth reached — return last narrative or tool results
        if (lastNarrative != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var scopedGame = await scopedContext.Games.FindAsync(game.Id);
            if (scopedGame != null)
            {
                scopedGame.LastGMAction = $"OpenNarrative (with {allToolCalls.Count} tool calls)";
                scopedGame.LastGMActionAt = DateTime.UtcNow;
                await scopedContext.SaveChangesAsync();
            }

            await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
            {
                GameId = game.Id,
                Status = game.GMStatus,
                LastAction = $"OpenNarrative (with {allToolCalls.Count} tool calls)",
                ChangedAt = DateTime.UtcNow
            });

            _logger.LogInformation("[OPEN_NARRATIVE] Complete | GameId={GameId} | FinalDepth={Depth} | TotalToolCalls={TotalCalls} | NarrativeLen={NarrativeLen}",
                game.Id, currentDepth, allToolCalls.Count, lastNarrative.Length);

            return lastNarrative;
        }

        // Max depth reached with no final narrative — return tool results
        _logger.LogWarning("[OPEN_NARRATIVE] MaxDepthReached | GameId={GameId} | MaxDepth={MaxDepth} | TotalToolCalls={TotalCalls} | LastToolResults={LastResults}",
            game.Id, _toolCallingMaxDepth, allToolCalls.Count,
            string.Join("\n", toolResults.Select(tr => $"  {tr.toolCallId}: {tr.message}")));

        using var scope2 = _scopeFactory.CreateScope();
        var scopedContext2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var scopedGame2 = await scopedContext2.Games.FindAsync(game.Id);
        if (scopedGame2 != null)
        {
            scopedGame2.LastGMAction = $"OpenNarrative (max depth {_toolCallingMaxDepth} reached)";
            scopedGame2.LastGMActionAt = DateTime.UtcNow;
            await scopedContext2.SaveChangesAsync();
        }

        return $"Tool calling reached max depth ({_toolCallingMaxDepth}). Last tool results:\n{string.Join("\n", toolResults.Select(tr => $"  {tr.toolCallId}: {tr.message} = {tr.result}"))}";
    }

    public async Task<AgentCall> ActivateGameAgentAsync(Guid gameId, Guid? creatorId)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException("Cannot activate GM agent: no LLM preset configured.");

        // Activate the GM agent
        game.GMStatus = GMStatus.Running;
        await _context.SaveChangesAsync();

        // Generate opening narrative
        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.System,
            ToAgent = AgentType.GM,
            Action = AgentAction.Narrate,
            Input = JsonSerializer.Serialize(new GMDispatchOptions
            {
                SystemPrompt = $"You are the Game Master for a TTRPG session. " +
                    $"Game system: {game.SystemId}. " +
                    $"Plot seed: {game.PlotSeed ?? "None"}. " +
                    $"Game parameters: {game.GameParameters ?? "None"}. " +
                    $"Create an immersive opening narrative that introduces the world, sets the tone, " +
                    $"and invites the players into the story. Be vivid and engaging." +
                    (string.IsNullOrEmpty(game.Language) || game.Language == "English" ? "" :
                        $"\n\n**Language**: All narrative output must be in **{game.Language}**. Write your response entirely in {game.Language}. Do NOT use English for any narrative content."),
                UserPrompt = "Generate the opening narrative for this game session."
            }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("GM agent activated for game {GameId} by creator {CreatorId}", gameId, creatorId);

        return call;
    }

    public async Task<AgentCall> SendSwayAsync(Guid gameId, Guid creatorId, string direction)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.CreatorId != creatorId)
            throw new UnauthorizedAccessException("Only the game creator can sway the story.");

        if (game.GMStatus != GMStatus.Running)
            throw new InvalidOperationException("Cannot sway: GM agent is not running.");

        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.Creator,
            ToAgent = AgentType.GM,
            Action = AgentAction.Nudge,
            Input = direction,
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Creator {CreatorId} sent narrative sway to game {GameId}: {Direction}",
            creatorId, gameId, direction);

        return call;
    }

    public async Task<(GMStatus Status, string? LastAction, DateTime? LastActionAt)> GetGMStatusAsync(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        return (game.GMStatus, game.LastGMAction, game.LastGMActionAt);
    }

    public async Task PauseGMAsync(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        game.GMStatus = GMStatus.Paused;
        game.LastGMAction = "Paused";
        game.LastGMActionAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("GM agent paused for game {GameId}", gameId);
    }

    public async Task ResumeGMAsync(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        game.GMStatus = GMStatus.Running;
        game.LastGMAction = "Resumed";
        game.LastGMActionAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("GM agent resumed for game {GameId}", gameId);
    }

    /// <summary>
    /// Persist a GM narrative as a Message entity and broadcast it to all players via SignalR.
    /// Called automatically after Narrate/Generate agent calls complete.
    /// </summary>
    private async Task BroadcastNarrationAsync(Guid gameId, string narrative)
    {
        try
        {
            // Truncate very long narratives to avoid message size limits
            var content = narrative.Length > 10000 ? narrative[..10000] : narrative;

            // Resolve the game's current session
            var session = await _context.Games
                .Where(g => g.Id == gameId)
                .Select(g => g.CurrentSessionId)
                .FirstOrDefaultAsync();

            var message = new Message
            {
                Id = Guid.NewGuid(),
                SessionId = session ?? Guid.Empty,
                PlayerId = null,
                Content = content,
                Type = Adnd.Server.Models.MessageType.GM,
                IsOOC = false,
                Metadata = JsonSerializer.SerializeToElement(new { source = "agent", action = "narrate" }),
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Emit event so GameLifecycleHandler can transition Starting → Active
            await _eventBus.PublishAsync(new GameNarrationStarted(gameId, message.Id));

            // Broadcast to all players in the game
            await _hubContext.Clients.Group(gameId.ToString()).SendAsync("NewMessage", new
            {
                message.Id,
                message.SessionId,
                message.PlayerId,
                message.Content,
                message.Type,
                message.Metadata,
                message.IsOOC,
                WhisperFromId = (Guid?)null,
                WhisperToId = (Guid?)null,
                WhisperTarget = (string?)null,
                message.CreatedAt
            });

            _logger.LogInformation("[NARRATION] Broadcast | GameId={GameId} | MessageId={MessageId} | Length={Length}",
                gameId, message.Id, content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NARRATION] Failed to broadcast narration for game {GameId}", gameId);
        }
    }

    /// <summary>
    /// Get a system definition for an agent call — checks custom system first, then built-ins.
    /// </summary>
    private SystemDefinition? GetSystemForCall(AgentCall call, string systemId)
    {
        // Check for a custom system on this game
        if (call.Game?.CustomSystemJson != null)
        {
            var custom = SystemRegistry.DeserializeCustom(call.Game.CustomSystemJson);
            if (custom != null)
                return custom;
        }

        return SystemRegistry.GetBuiltIn(systemId);
    }
}

// ============= Dispatch option classes =============

public class LLMDispatchOptions
{
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public LLMOptions? Options { get; set; }
}

public class DiceDispatchOptions
{
    public string Formula { get; set; } = string.Empty;
    public Guid? PlayerId { get; set; }
}

public class RAGDispatchOptions
{
    public int MessageCount { get; set; } = 20;
    public string? Query { get; set; }
    public int Limit { get; set; } = 5;
}

public class NPCDispatchOptions
{
    public Guid NpcId { get; set; }
}

public class PlayerDispatchOptions
{
    public Guid CharacterId { get; set; }
}

public class SystemDispatchOptions
{
    public string SystemId { get; set; } = string.Empty;
}

public class GMDispatchOptions
{
    public string? StateJson { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public LLMOptions? Options { get; set; }
}

public class CharacterCreateOptions
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int? Level { get; set; }
    public int? CurrentHP { get; set; }
    public int? MaxHP { get; set; }
    public object? Attributes { get; set; }
    public object? Skills { get; set; }
    public object? Inventory { get; set; }
    public string? SystemId { get; set; }
}
