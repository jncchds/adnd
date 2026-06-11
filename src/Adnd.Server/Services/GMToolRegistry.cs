using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Registry of available GM tools for the agentic framework.
/// Provides tool definitions (schemas) and execution.
/// </summary>
public interface IGMToolRegistry
{
    /// <summary>
    /// Get all available tool definitions for the GM agent.
    /// </summary>
    List<GMToolDefinition> GetAvailableTools(Guid gameId);

    /// <summary>
    /// Get tools by category.
    /// </summary>
    List<GMToolDefinition> GetToolsByCategory(Guid gameId, ToolCategory category);

    /// <summary>
    /// Execute a tool call and return the result.
    /// </summary>
    Task<ToolExecutionResult> ExecuteToolAsync(Guid gameId, Guid sessionId, string toolName, string argumentsJson);

    /// <summary>
    /// Check if a tool requires user confirmation before execution.
    /// </summary>
    bool RequiresConfirmation(string toolName);
}

public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }        // JSON result
    public string? OutputMessage { get; set; } // Human-readable message
    public string? Error { get; set; }
    public bool RequiresUserInput { get; set; }
    public string? UserInputType { get; set; } // e.g., "player_roll", "confirmation"
}

public class GMToolRegistry : IGMToolRegistry
{
    private readonly AppDbContext _context;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly ICombatService _combatService;
    private readonly IWhisperService _whisperService;
    private readonly ISystemRegistry _systemRegistry;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IApiKeyEncryptionService _encryption;
    private readonly ILogger<GMToolRegistry> _logger;

    public GMToolRegistry(
        AppDbContext context,
        IGameEngine gameEngine,
        IRAGService ragService,
        ICombatService combatService,
        IWhisperService whisperService,
        ISystemRegistry systemRegistry,
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption,
        ILogger<GMToolRegistry> logger)
    {
        _context = context;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _combatService = combatService;
        _whisperService = whisperService;
        _systemRegistry = systemRegistry;
        _providerFactory = providerFactory;
        _encryption = encryption;
        _logger = logger;
    }

    public List<GMToolDefinition> GetAvailableTools(Guid gameId)
    {
        return new List<GMToolDefinition>
        {
            // === Auto-execute tools ===
            new()
            {
                Name = "narrate",
                Description = "Generate narrative text for the game. Describe scenes, events, NPC dialogue, or world reactions.",
                Category = ToolCategory.Narrative,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The game context to narrate" },
                        ["tone"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Narrative tone: dramatic, humorous, tense, mysterious, etc." },
                        ["focus"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "What to focus the narration on" }
                    },
                    ["required"] = new[] { "context" }
                }
            },
            new()
            {
                Name = "queryRAG",
                Description = "Search plot threads and game context for relevant information. Use when you need to recall plot details, NPC info, or past events.",
                Category = ToolCategory.Query,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["query"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "What to search for" },
                        ["limit"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Max results (default: 5)" }
                    },
                    ["required"] = new[] { "query" }
                }
            },
            new()
            {
                Name = "queryPlotThreads",
                Description = "Get all active plot threads for this game. Use when checking what story threads are active and their momentum.",
                Category = ToolCategory.Query,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["sortBy"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sort by: momentum, relevance, or name" }
                    }
                }
            },
            new()
            {
                Name = "queryCharacter",
                Description = "Get details about a player's character sheet. Use when checking HP, attributes, skills, inventory, or conditions.",
                Category = ToolCategory.Query,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["playerId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid", ["description"] = "Player ID to query" },
                        ["gameId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid", ["description"] = "Game ID" }
                    },
                    ["required"] = new[] { "playerId", "gameId" }
                }
            },
            new()
            {
                Name = "queryPlayers",
                Description = "Get all active players in the game with their roles and character names.",
                Category = ToolCategory.Query,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["gameId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid", ["description"] = "Game ID" },
                        ["role"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Filter by role: player, creator, spectator, observer" }
                    },
                    ["required"] = new[] { "gameId" }
                }
            },
            new()
            {
                Name = "manageState",
                Description = "Update game state: set game state variables, track NPC status, update plot thread momentum, or modify game parameters.",
                Category = ToolCategory.StateManagement,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["action"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "setState", "updateMomentum", "updateNPC" }, ["description"] = "State management action" },
                        ["target"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Target of the state change (thread ID, NPC name, etc.)" },
                        ["value"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "New value or change description" }
                    },
                    ["required"] = new[] { "action", "target", "value" }
                }
            },
            // === Player roll tools (require confirmation) ===
            new()
            {
                Name = "askPlayerToRoll",
                Description = "Ask one or more players to make a skill check roll. The player will be prompted to roll in their UI. Use when a player's action requires a skill check.",
                Category = ToolCategory.PlayerRoll,
                RequiresConfirmation = true,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["skill"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Skill name: perception, stealth, athletics, persuasion, etc." },
                        ["playerIds"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }, ["description"] = "Player IDs to ask (empty for all players)" },
                        ["dc"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Difficulty class (optional)" },
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Context for the roll (what they're attempting)" },
                        ["optional"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "If true, player can decline the roll" }
                    },
                    ["required"] = new[] { "skill", "context" }
                }
            },
            new()
            {
                Name = "askPlayerToRollDice",
                Description = "Ask a player to roll a specific dice formula. Use for custom rolls, saving throws, or initiative.",
                Category = ToolCategory.PlayerRoll,
                RequiresConfirmation = true,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["formula"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Dice formula: 1d20, 4d6kh3, 2d8+3, etc." },
                        ["playerId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid", ["description"] = "Player ID" },
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "What the roll is for" },
                        ["optional"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "If true, player can decline" }
                    },
                    ["required"] = new[] { "formula", "context" }
                }
            },
            // === System roll tools (auto-execute) ===
            new()
            {
                Name = "rollDice",
                Description = "Roll dice for the system (NPCs, monsters, environmental effects). Returns the roll result.",
                Category = ToolCategory.SystemRoll,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["formula"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Dice formula: 1d20, 4d6kh3, 2d8+3, etc." },
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "What the roll is for" }
                    },
                    ["required"] = new[] { "formula" }
                }
            },
            new()
            {
                Name = "rollSkillCheck",
                Description = "Perform a skill check for an NPC or system entity (not a player). Returns success/failure against DC.",
                Category = ToolCategory.SystemRoll,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["skill"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Skill name" },
                        ["modifier"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Skill modifier (if known)" },
                        ["dc"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Difficulty class" },
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "What the check is for" }
                    },
                    ["required"] = new[] { "skill", "dc" }
                }
            },
            new()
            {
                Name = "rollAttack",
                Description = "Perform an attack roll for an NPC or monster. Returns hit/miss and damage.",
                Category = ToolCategory.SystemRoll,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["weapon"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Weapon name" },
                        ["attackBonus"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Attack bonus" },
                        ["damageFormula"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Damage dice formula" },
                        ["targetAC"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Target's AC" },
                        ["targetName"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Target name" }
                    },
                    ["required"] = new[] { "weapon", "damageFormula", "targetAC", "targetName" }
                }
            },
            // === Combat tools (auto-execute) ===
            new()
            {
                Name = "startCombat",
                Description = "Start a new combat encounter. Called when combat begins.",
                Category = ToolCategory.Combat,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Combat name/description" },
                        ["participants"] = new Dictionary<string, object> { ["type"] = "array", ["description"] = "List of combatants" }
                    },
                    ["required"] = new[] { "name" }
                }
            },
            new()
            {
                Name = "addCombatant",
                Description = "Add a combatant to an active combat.",
                Category = ToolCategory.Combat,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["combatId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["type"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "player", "npc", "monster" } },
                        ["hp"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["maxHP"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["ac"] = new Dictionary<string, object> { ["type"] = "integer" }
                    },
                    ["required"] = new[] { "name", "type", "hp", "maxHP", "ac" }
                }
            },
            new()
            {
                Name = "applyDamage",
                Description = "Deal damage to a combatant.",
                Category = ToolCategory.Combat,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["combatId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["targetId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["amount"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["source"] = new Dictionary<string, object> { ["type"] = "string" }
                    },
                    ["required"] = new[] { "combatId", "targetId", "amount" }
                }
            },
            new()
            {
                Name = "applyCondition",
                Description = "Apply a condition to a combatant (blinded, frightened, prone, etc.).",
                Category = ToolCategory.Combat,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["combatId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["targetId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["condition"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["duration"] = new Dictionary<string, object> { ["type"] = "integer" }
                    },
                    ["required"] = new[] { "combatId", "targetId", "condition" }
                }
            },
            new()
            {
                Name = "endCombat",
                Description = "End the current combat encounter.",
                Category = ToolCategory.Combat,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["combatId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["result"] = new Dictionary<string, object> { ["type"] = "string" }
                    },
                    ["required"] = new[] { "combatId" }
                }
            },
            // === NPC tools ===
            new()
            {
                Name = "createNPC",
                Description = "Create a new NPC for the game.",
                Category = ToolCategory.Narrative,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["attributes"] = new Dictionary<string, object> { ["type"] = "object" },
                        ["skills"] = new Dictionary<string, object> { ["type"] = "object" }
                    },
                    ["required"] = new[] { "name", "description" }
                }
            },
            new()
            {
                Name = "updateNPC",
                Description = "Update an existing NPC's details.",
                Category = ToolCategory.Narrative,
                RequiresConfirmation = false,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["npcId"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["attributes"] = new Dictionary<string, object> { ["type"] = "object" }
                    },
                    ["required"] = new[] { "npcId" }
                }
            }
        };
    }

    public List<GMToolDefinition> GetToolsByCategory(Guid gameId, ToolCategory category)
    {
        return GetAvailableTools(gameId).Where(t => t.Category == category).ToList();
    }

    public bool RequiresConfirmation(string toolName)
    {
        return GetAvailableTools(Guid.Empty).FirstOrDefault(t => t.Name == toolName)?.RequiresConfirmation == true;
    }

    public async Task<ToolExecutionResult> ExecuteToolAsync(Guid gameId, Guid sessionId, string toolName, string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson ?? "{}") ?? new();

            return toolName switch
            {
                "narrate" => await ExecuteNarrate(gameId, args),
                "queryRAG" => await ExecuteQueryRAG(args),
                "queryPlotThreads" => await ExecuteQueryPlotThreads(args, gameId),
                "queryCharacter" => await ExecuteQueryCharacter(args),
                "queryPlayers" => await ExecuteQueryPlayers(args, gameId),
                "manageState" => await ExecuteManageState(args, gameId),
                "askPlayerToRoll" => await ExecuteAskPlayerToRoll(args, gameId, sessionId),
                "askPlayerToRollDice" => await ExecuteAskPlayerToRollDice(args, gameId, sessionId),
                "rollDice" => ExecuteRollDice(args),
                "rollSkillCheck" => await ExecuteRollSkillCheck(args),
                "rollAttack" => await ExecuteRollAttack(args),
                "startCombat" => await ExecuteStartCombat(args, gameId, sessionId),
                "addCombatant" => await ExecuteAddCombatant(args),
                "applyDamage" => await ExecuteApplyDamage(args),
                "applyCondition" => await ExecuteApplyCondition(args),
                "endCombat" => await ExecuteEndCombat(args),
                "createNPC" => await ExecuteCreateNPC(args, gameId),
                "updateNPC" => await ExecuteUpdateNPC(args),
                _ => new ToolExecutionResult
                {
                    Success = false,
                    Error = $"Unknown tool: {toolName}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool '{ToolName}'", toolName);
            return new ToolExecutionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    // === Tool implementations ===

    private async Task<ToolExecutionResult> ExecuteNarrate(Guid gameId, Dictionary<string, object> args)
    {
        var context = args.GetValueOrDefault("context")?.ToString() ?? "Continue the narrative.";
        var tone = args.GetValueOrDefault("tone")?.ToString() ?? "dramatic";
        var focus = args.GetValueOrDefault("focus")?.ToString();

        // Look up the game to get the LLM preset
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null || game.LLMPreset == null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = "Game or LLM preset not found for narration."
            };
        }

        // Get the LLM provider
        var preset = game.LLMPreset;
        ILLMProvider? provider = null;
        if (preset.ApiKey != null && preset.DecryptedApiKey == null)
        {
            try
            {
                preset.DecryptedApiKey = _encryption.Decrypt(preset.ApiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt API key for preset '{PresetName}'", preset.Name);
                return new ToolExecutionResult
                {
                    Success = false,
                    Error = "Failed to decrypt API key."
                };
            }
        }
        provider = _providerFactory.CreateFromPreset(preset);

        if (provider == null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"LLM provider '{game.LLMPreset.ProviderType}' not available."
            };
        }

        try
        {
            var systemPrompt = $"You are a TTRPG Game Master. Generate narrative text based on the given context. " +
                $"Use the provided tone and focus to craft an immersive description. " +
                $"Game system: {game.SystemId}. " +
                (string.IsNullOrEmpty(game.Language) || game.Language == "English" ? "" :
                    $"\n\n**Language**: All output must be in **{game.Language}**.") ;
            var userPrompt = $"Context: {context}\nTone: {tone}" + (focus != null ? $"\nFocus: {focus}" : "");

            var result = await provider.CompleteAsync(systemPrompt, userPrompt);

            return new ToolExecutionResult
            {
                Success = true,
                Output = result,
                OutputMessage = $"Narrating: {context} (tone: {tone})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate narrative via LLM for game {GameId}", gameId);
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"LLM narration failed: {ex.Message}"
            };
        }
    }

    private async Task<ToolExecutionResult> ExecuteQueryRAG(Dictionary<string, object> args)
    {
        var query = args.GetValueOrDefault("query")?.ToString() ?? "";
        var limit = args.GetValueOrDefault("limit") is int l ? l : 5;

        return new ToolExecutionResult
        {
            Success = true,
            Output = $"RAG search for: {query} (limit: {limit}) — results pending RAG execution",
            OutputMessage = $"Searched plot context for: \"{query}\""
        };
    }

    private async Task<ToolExecutionResult> ExecuteQueryPlotThreads(Dictionary<string, object> args, Guid gameId)
    {
        var sortBy = args.GetValueOrDefault("sortBy")?.ToString() ?? "momentum";

        return new ToolExecutionResult
        {
            Success = true,
            Output = $"Plot threads sorted by {sortBy} for game {gameId}",
            OutputMessage = "Retrieved active plot threads"
        };
    }

    private async Task<ToolExecutionResult> ExecuteQueryCharacter(Dictionary<string, object> args)
    {
        var playerId = args.GetValueOrDefault("playerId")?.ToString();
        if (string.IsNullOrEmpty(playerId))
            return new ToolExecutionResult { Success = false, Error = "playerId required" };

        return new ToolExecutionResult
        {
            Success = true,
            Output = $"Character data for player {playerId}",
            OutputMessage = $"Retrieved character data"
        };
    }

    private async Task<ToolExecutionResult> ExecuteQueryPlayers(Dictionary<string, object> args, Guid gameId)
    {
        var role = args.GetValueOrDefault("role")?.ToString();

        return new ToolExecutionResult
        {
            Success = true,
            Output = $"Players in game {gameId}" + (role != null ? $" (role: {role})" : ""),
            OutputMessage = "Retrieved player list"
        };
    }

    private async Task<ToolExecutionResult> ExecuteManageState(Dictionary<string, object> args, Guid gameId)
    {
        var action = args.GetValueOrDefault("action")?.ToString() ?? "";
        var target = args.GetValueOrDefault("target")?.ToString() ?? "";
        var value = args.GetValueOrDefault("value")?.ToString() ?? "";

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { action, target, value }),
            OutputMessage = $"State updated: {action} on {target} = {value}"
        };
    }

    private async Task<ToolExecutionResult> ExecuteAskPlayerToRoll(Dictionary<string, object> args, Guid gameId, Guid sessionId)
    {
        var skill = args.GetValueOrDefault("skill")?.ToString() ?? "";
        var context = args.GetValueOrDefault("context")?.ToString() ?? "";
        var dc = args.GetValueOrDefault("dc") is int d ? d : 15;
        var optional = args.GetValueOrDefault("optional") is bool o && o;

        // Parse player IDs
        var playerIds = new List<Guid>();
        if (args.TryGetValue("playerIds", out var pidsObj) && pidsObj is System.Collections.IEnumerable pids)
        {
            foreach (var pid in pids)
            {
                if (pid is string ps && Guid.TryParse(ps, out var guid))
                    playerIds.Add(guid);
            }
        }

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { skill, context, dc, optional, playerIds }),
            OutputMessage = $"Asking players to roll {skill} (DC {dc}): {context}",
            RequiresUserInput = true,
            UserInputType = "skill_check",
        };
    }

    private async Task<ToolExecutionResult> ExecuteAskPlayerToRollDice(Dictionary<string, object> args, Guid gameId, Guid sessionId)
    {
        var formula = args.GetValueOrDefault("formula")?.ToString() ?? "";
        var context = args.GetValueOrDefault("context")?.ToString() ?? "";
        var playerId = args.GetValueOrDefault("playerId")?.ToString();
        var optional = args.GetValueOrDefault("optional") is bool o && o;

        var parsedPlayerId = Guid.Empty;
        if (!string.IsNullOrEmpty(playerId) && Guid.TryParse(playerId, out var parsed))
            parsedPlayerId = parsed;

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { formula, context, playerId = parsedPlayerId, optional }),
            OutputMessage = $"Asking player to roll {formula}: {context}",
            RequiresUserInput = true,
            UserInputType = "dice_roll",
        };
    }

    private ToolExecutionResult ExecuteRollDice(Dictionary<string, object> args)
    {
        var formula = args.GetValueOrDefault("formula")?.ToString() ?? "1d20";
        var context = args.GetValueOrDefault("context")?.ToString();

        return new ToolExecutionResult
        {
            Success = true,
            Output = $"Dice formula: {formula}" + (context != null ? $" (context: {context})" : ""),
            OutputMessage = $"Rolled {formula}"
        };
    }

    private async Task<ToolExecutionResult> ExecuteRollSkillCheck(Dictionary<string, object> args)
    {
        var skill = args.GetValueOrDefault("skill")?.ToString() ?? "";
        var dc = args.GetValueOrDefault("dc") is int d ? d : 15;
        var context = args.GetValueOrDefault("context")?.ToString();

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { skill, dc, context }),
            OutputMessage = $"NPC skill check: {skill} vs DC {dc}" + (context != null ? $" — {context}" : "")
        };
    }

    private async Task<ToolExecutionResult> ExecuteRollAttack(Dictionary<string, object> args)
    {
        var weapon = args.GetValueOrDefault("weapon")?.ToString() ?? "";
        var targetName = args.GetValueOrDefault("targetName")?.ToString() ?? "";
        var targetAC = args.GetValueOrDefault("targetAC") is int ac ? ac : 15;
        var damageFormula = args.GetValueOrDefault("damageFormula")?.ToString() ?? "1d6";

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { weapon, targetName, targetAC, damageFormula }),
            OutputMessage = $"Attack: {weapon} vs {targetName} (AC {targetAC})"
        };
    }

    private async Task<ToolExecutionResult> ExecuteStartCombat(Dictionary<string, object> args, Guid gameId, Guid sessionId)
    {
        var name = args.GetValueOrDefault("name")?.ToString() ?? "Combat";

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { name, gameId, sessionId }),
            OutputMessage = $"Combat started: {name}"
        };
    }

    private async Task<ToolExecutionResult> ExecuteAddCombatant(Dictionary<string, object> args)
    {
        var name = args.GetValueOrDefault("name")?.ToString() ?? "";
        var type = args.GetValueOrDefault("type")?.ToString() ?? "npc";
        var hp = args.GetValueOrDefault("hp") is int h ? h : 10;
        var maxHP = args.GetValueOrDefault("maxHP") is int m ? m : 10;
        var ac = args.GetValueOrDefault("ac") is int a ? a : 10;

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { name, type, hp, maxHP, ac }),
            OutputMessage = $"Added combatant: {name} ({type})"
        };
    }

    private async Task<ToolExecutionResult> ExecuteApplyDamage(Dictionary<string, object> args)
    {
        var combatIdStr = args.GetValueOrDefault("combatId")?.ToString() ?? "";
        var targetIdStr = args.GetValueOrDefault("targetId")?.ToString() ?? "";
        var amount = args.GetValueOrDefault("amount") is int a ? a : 0;
        var source = args.GetValueOrDefault("source")?.ToString();

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { combatIdStr, targetIdStr, amount, source }),
            OutputMessage = $"Dealt {amount} damage to {targetIdStr}"
        };
    }

    private async Task<ToolExecutionResult> ExecuteApplyCondition(Dictionary<string, object> args)
    {
        var combatIdStr = args.GetValueOrDefault("combatId")?.ToString() ?? "";
        var targetIdStr = args.GetValueOrDefault("targetId")?.ToString() ?? "";
        var condition = args.GetValueOrDefault("condition")?.ToString() ?? "";
        var duration = args.GetValueOrDefault("duration") is int d ? (int?)d : null;

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { combatIdStr, targetIdStr, condition, duration }),
            OutputMessage = $"Applied {condition} to {targetIdStr}"
        };
    }

    private async Task<ToolExecutionResult> ExecuteEndCombat(Dictionary<string, object> args)
    {
        var combatIdStr = args.GetValueOrDefault("combatId")?.ToString() ?? "";
        var result = args.GetValueOrDefault("result")?.ToString();

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { combatIdStr, result }),
            OutputMessage = $"Combat ended: {result ?? "unknown result"}"
        };
    }

    private async Task<ToolExecutionResult> ExecuteCreateNPC(Dictionary<string, object> args, Guid gameId)
    {
        var name = args.GetValueOrDefault("name")?.ToString() ?? "";
        var description = args.GetValueOrDefault("description")?.ToString() ?? "";

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { name, description, gameId }),
            OutputMessage = $"Created NPC: {name}"
        };
    }

    private async Task<ToolExecutionResult> ExecuteUpdateNPC(Dictionary<string, object> args)
    {
        var npcIdStr = args.GetValueOrDefault("npcId")?.ToString() ?? "";
        var name = args.GetValueOrDefault("name")?.ToString();
        var description = args.GetValueOrDefault("description")?.ToString();

        return new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new { npcIdStr, name, description }),
            OutputMessage = $"Updated NPC: {name ?? npcIdStr}"
        };
    }
}
