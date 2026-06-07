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
}

public class AgentBus : IAgentBus
{
    private readonly AppDbContext _context;
    private readonly ILLMProviderRegistry _llmRegistry;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly IDiceEngine _diceEngine;
    private readonly ISystemRegistry _systemRegistry;
    private readonly ILogger<AgentBus> _logger;

    public AgentBus(
        AppDbContext context,
        ILLMProviderRegistry llmRegistry,
        IGameEngine gameEngine,
        IRAGService ragService,
        IDiceEngine diceEngine,
        ISystemRegistry systemRegistry,
        ILogger<AgentBus> logger)
    {
        _context = context;
        _llmRegistry = llmRegistry;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _diceEngine = diceEngine;
        _systemRegistry = systemRegistry;
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
            _ => throw new ArgumentException($"Unknown agent type: {call.ToAgent}")
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
            if (call.Action == AgentAction.Generate || call.Action == AgentAction.Narrate)
            {
                var options = JsonSerializer.Deserialize<LLMDispatchOptions>(call.Input)
                    ?? new LLMDispatchOptions();

                var systemPrompt = options.SystemPrompt ?? "You are a TTRPG Game Master assistant.";
                var userPrompt = options.UserPrompt ?? call.Input ?? "Generate content.";

                var result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
                return result;
            }

            if (call.Action == AgentAction.Suggest)
            {
                var options = JsonSerializer.Deserialize<LLMDispatchOptions>(call.Input)
                    ?? new LLMDispatchOptions();

                var systemPrompt = "You are a creative TTRPG Game Master assistant. " +
                    "Provide engaging plot suggestions based on the game context. " +
                    "Respond with a JSON array of suggestions.";

                var userPrompt = options.UserPrompt ?? call.Input ?? "Suggest plot continuations.";

                var result = await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);

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
                var options = JsonSerializer.Deserialize<LLMDispatchOptions>(call.Input)
                    ?? new LLMDispatchOptions();

                var systemPrompt = "You are a helpful TTRPG assistant. Answer questions about the game state, rules, and lore.";
                var userPrompt = options.UserPrompt ?? call.Input ?? "Answer the question.";

                return await provider.CompleteAsync(systemPrompt, userPrompt, options.Options);
            }

            return "LLM: action not handled.";
        }
        catch (Exception ex)
        {
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
                    JsonSerializer.Serialize(await _ragService.FindSimilarPlotThreadsAsync(call.GameId, options.Query, options.Limit)),
                AgentAction.Generate =>
                    JsonSerializer.Serialize(await _ragService.GeneratePlotContextAsync(call.GameId, options.MessageCount)),
                AgentAction.Suggest =>
                    JsonSerializer.Serialize(await _ragService.SuggestContinuationAsync(call.SessionId ?? Guid.Empty, options.Query)),
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
            var options = JsonSerializer.Deserialize<NPCDispatchOptions>(call.Input)
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
            var options = JsonSerializer.Deserialize<PlayerDispatchOptions>(call.Input)
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

    private async Task<string> HandleSystemCall(AgentCall call)
    {
        try
        {
            var options = JsonSerializer.Deserialize<SystemDispatchOptions>(call.Input)
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
        var options = JsonSerializer.Deserialize<GMDispatchOptions>(call.Input)
            ?? new GMDispatchOptions();

        if (call.Action == AgentAction.ManageState)
        {
            var game = await _context.Games.FindAsync(call.GameId);
            if (game == null)
                return "Game not found.";

            game.GameState = options.StateJson ?? game.GameState;
            await _context.SaveChangesAsync();

            return "Game state updated.";
        }

        if (call.Action == AgentAction.Notify)
        {
            // In a full implementation, this would notify other agents
            return "Notification sent to relevant agents.";
        }

        return "GM agent: action not handled.";
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
}
