using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
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
    private readonly ILLMProviderRegistry _llmRegistry;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly IDiceEngine _diceEngine;
    private readonly ISystemRegistry _systemRegistry;
    private readonly ILLMPresetService _presetService;
    private readonly ILLMInteractionLogger _interactionLogger;
    private readonly IGMToolRegistry _toolRegistry;
    private readonly ILogger<AgentBus> _logger;

    public AgentBus(
        AppDbContext context,
        ILLMProviderRegistry llmRegistry,
        IGameEngine gameEngine,
        IRAGService ragService,
        IDiceEngine diceEngine,
        ISystemRegistry systemRegistry,
        ILLMPresetService presetService,
        ILLMInteractionLogger interactionLogger,
        IGMToolRegistry toolRegistry,
        ILogger<AgentBus> logger)
    {
        _context = context;
        _llmRegistry = llmRegistry;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _diceEngine = diceEngine;
        _systemRegistry = systemRegistry;
        _presetService = presetService;
        _interactionLogger = interactionLogger;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public async Task<AgentCall> SendCallAsync(AgentCall call)
    {
        call.Status = AgentCallStatus.Pending;
        call.CreatedAt = DateTime.UtcNow;

        _context.AgentCalls.Add(call);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Agent call queued: {FromAgent} -> {ToAgent} [{Action}] (call: {CallId})",
            call.FromAgent, call.ToAgent, call.Action, call.Id);

        return call;
    }

    public async Task<AgentCall> ExecuteCallAsync(AgentCall call)
    {
        call.Status = AgentCallStatus.Running;
        call.StartedAt = DateTime.UtcNow;
        _context.AgentCalls.Update(call);
        await _context.SaveChangesAsync();

        try
        {
            var result = await DispatchCall(call);

            call.Status = AgentCallStatus.Completed;
            call.Output = result;
            call.CompletedAt = DateTime.UtcNow;
            var callCompletedAt = call.CompletedAt.Value;
            var callStartedAt = call.StartedAt ?? call.CreatedAt;
            call.DurationMs = (int)(callCompletedAt - callStartedAt).TotalMilliseconds;

            _context.AgentCalls.Update(call);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Agent call completed: {FromAgent} -> {ToAgent} [{Action}] in {Duration}ms",
                call.FromAgent, call.ToAgent, call.Action, call.DurationMs);

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

            _context.AgentCalls.Update(call);
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Agent call failed: {FromAgent} -> {ToAgent} [{Action}]",
                call.FromAgent, call.ToAgent, call.Action);

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
        var provider = _llmRegistry.GetProvider("ollama");
        if (provider == null)
        {
            return "LLM provider not available.";
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

                var sw = System.Diagnostics.Stopwatch.StartNew();
                result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
                sw.Stop();

                // Log successful interaction
                var model = options?.Options?.Model ?? "default";
                var tokenUsage = provider.GetTokenUsage(result);
                await _interactionLogger.LogInteractionAsync(
                    Guid.Empty, null, provider.ProviderId, model,
                    tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                    (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, result,
                    null, null, "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString());

                return result;
            }

            if (call.Action == AgentAction.Suggest)
            {
                var options = JsonSerializer.Deserialize<LLMDispatchOptions>(call.Input ?? "{}")
                    ?? new LLMDispatchOptions();

                var systemPrompt = "You are a creative TTRPG Game Master assistant. " +
                    "Provide engaging plot suggestions based on the game context. " +
                    "Respond with a JSON array of suggestions.";

                var userPrompt = options.UserPrompt ?? call.Input ?? "Suggest plot continuations.";

                var sw = System.Diagnostics.Stopwatch.StartNew();
                result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
                sw.Stop();

                var model = options?.Options?.Model ?? "default";
                var tokenUsage = provider.GetTokenUsage(result);
                await _interactionLogger.LogInteractionAsync(
                    Guid.Empty, null, provider.ProviderId, model,
                    tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                    (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, result,
                    null, null, "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString());

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
                    Guid.Empty, null, provider.ProviderId, model,
                    tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                    (int)sw.ElapsedMilliseconds, systemPrompt, userPrompt, result,
                    null, null, "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString());

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
                    Guid.Empty, null, "unknown", "unknown", null, null, null,
                    0, ex.ToString(),
                    "agent", call.GameId, call.SessionId,
                    "AgentBus", call.Action.ToString());
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

            var npc = await _context.NPCs.FindAsync(options.NpcId);
            if (npc == null)
                return "NPC not found.";

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
                var character = await _context.Characters.FindAsync(options.CharacterId);
                if (character == null)
                    return "Character not found.";

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

            var player = await _context.Players.FindAsync(playerId);
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

            _context.Characters.Add(character);
            await _context.SaveChangesAsync();

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

            var system = _systemRegistry.GetSystem(options.SystemId);
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

        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == call.GameId);
        if (game == null)
            return "Game not found.";

        if (call.Action == AgentAction.ManageState)
        {
            game.GameState = options.StateJson ?? game.GameState;
            game.LastGMAction = "ManageState";
            game.LastGMActionAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return "Game state updated.";
        }

        if (call.Action == AgentAction.Narrate || call.Action == AgentAction.Generate)
        {
            // Use the game's LLM preset to generate narrative with tool calling
            if (game.LLMPreset == null)
                return "No LLM preset configured for this game.";

            var provider = _llmRegistry.GetProvider(game.LLMPreset.ProviderType);
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
                "GM", call.Action.ToString());

            // Handle tool calls from the LLM
            if (completion.HasToolCalls)
            {
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
                        await _context.SaveChangesAsync();

                        // Return tool call info for frontend notification
                        return JsonSerializer.Serialize(new { toolCallId = toolCall.Id, toolName = toolCall.Name, waitingConfirmation = true });
                    }
                }

                // Feed tool results back to LLM for final narrative
                var toolResultsText = string.Join("\n", toolResults.Select(tr =>
                    $"Tool '{tr.toolCallId}': {tr.message}\nResult: {tr.result}"));

                var followUpPrompt = $"Tool results:\n{toolResultsText}\n\nNow continue the narrative based on these results.";

                var followUpCompletion = await provider.CompleteAsync(
                    systemPrompt, followUpPrompt, options.Options);

                game.LastGMAction = $"Narrate (with {completion.ToolCalls.Count} tool calls)";
                game.LastGMActionAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return followUpCompletion;
            }

            game.LastGMAction = "Narrate";
            game.LastGMActionAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return completion.Content;
        }

        if (call.Action == AgentAction.Nudge)
        {
            // Incorporate Creator's narrative direction into the story
            if (game.LLMPreset == null)
                return "No LLM preset configured for this game.";

            var provider = _llmRegistry.GetProvider(game.LLMPreset.ProviderType);
            if (provider == null)
                return $"LLM provider '{game.LLMPreset.ProviderType}' not available.";

            var systemPrompt = $"You are the Game Master for a TTRPG session. " +
                $"The game creator has sent a narrative nudge: {call.Input}. " +
                $"Incorporate this direction naturally into the ongoing story. " +
                $"Game system: {game.SystemId}. " +
                $"Current game state: {game.GameState ?? "None"}. " +
                $"Respond with an immersive narrative that follows the creator's direction.";

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
                "GM", call.Action.ToString());

            game.LastGMAction = "Nudge";
            game.LastGMActionAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return result;
        }

        if (call.Action == AgentAction.Notify)
        {
            return "Notification sent to relevant agents.";
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
        _context.GMToolCalls.Add(toolCallRecord);
        await _context.SaveChangesAsync();

        // Execute the tool
        var result = await _toolRegistry.ExecuteToolAsync(gameId, sessionId, toolName, args);

        toolCallRecord.Status = result.Success ? ToolCallStatus.Completed : ToolCallStatus.Failed;
        toolCallRecord.Result = result.Output;
        toolCallRecord.OutputMessage = result.OutputMessage;
        toolCallRecord.Error = result.Error;
        toolCallRecord.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // If tool requires user confirmation, flag it
        if (_toolRegistry.RequiresConfirmation(toolName) && result.Success)
        {
            toolCallRecord.Status = ToolCallStatus.WaitingConfirmation;
            await _context.SaveChangesAsync();

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
                    $"and invites the players into the story. Be vivid and engaging.",
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
