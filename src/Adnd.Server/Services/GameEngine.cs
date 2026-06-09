using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Core game engine that handles dice rolls, character operations,
/// skill checks, and system-specific rules.
/// </summary>
public interface IGameEngine
{
    Task<DiceRollResult> RollDiceAsync(Guid sessionId, string formula, Guid? playerId = null);
    Task<SkillCheckResult> SkillCheckAsync(Guid sessionId, string skill, Guid? playerId, int? dc = null);
    Task<AttackResult> AttackAsync(Guid sessionId, string weapon, string targetName, Guid? playerId = null);
    Task<Character> CreateCharacterAsync(Guid playerId, string systemId, JsonDocument? template = null);
    Task<Character> UpdateCharacterAsync(Guid characterId, JsonPatch patch);
    Task<Character?> GetCharacterAsync(Guid characterId);
    Task<JsonDocument> GetDefaultCharacterTemplateAsync(string systemId);
    Task<(bool valid, string[] errors)> ValidateCharacterAsync(Guid playerId, string systemId, JsonElement character);
}

public class GameEngine : IGameEngine
{
    private readonly AppDbContext _context;
    private readonly IDiceEngine _diceEngine;
    private readonly ISystemRegistry _systemRegistry;
    private readonly ISystemRulesFactory _rulesFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GameEngine> _logger;

    public GameEngine(
        AppDbContext context,
        IDiceEngine diceEngine,
        ISystemRegistry systemRegistry,
        ISystemRulesFactory rulesFactory,
        IEmbeddingService embeddingService,
        ILogger<GameEngine> logger)
    {
        _context = context;
        _diceEngine = diceEngine;
        _systemRegistry = systemRegistry;
        _rulesFactory = rulesFactory;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<DiceRollResult> RollDiceAsync(Guid sessionId, string formula, Guid? playerId = null)
    {
        // Verify session exists and is active
        var session = await _context.GameSessions
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null || session.Game == null)
            throw new InvalidOperationException($"Game session {sessionId} not found.");

        if (session.Game.Status != GameStatus.Active)
            throw new InvalidOperationException($"Game is not active (status: {session.Game.Status}).");

        var result = _diceEngine.Roll(formula, playerId);

        // Save the roll as a message in the session
        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = playerId,
            Content = _diceEngine.FormatResult(result),
            Type = MessageType.Dice,
            Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                formula = result.Formula,
                diceCount = result.DiceCount,
                diceType = result.DiceType,
                modifier = result.Modifier,
                rolls = result.Rolls,
                total = result.Total
            })).RootElement,
            CreatedAt = result.RolledAt
        };

        await _context.Messages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Generate embedding for the dice roll message
        await EmbedMessageAsync(sessionId, message.Id, message.Content);

        return result;
    }

    /// <summary>
    /// Generate an embedding for a message asynchronously.
    /// </summary>
    private async Task EmbedMessageAsync(Guid sessionId, Guid messageId, string content)
    {
        try
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null)
                return;

            var embedding = await _embeddingService.GenerateEmbeddingAsync(session.GameId, content);
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null && message.Embedding == null)
            {
                message.Embedding = embedding;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for message {MessageId} in session {SessionId}", messageId, sessionId);
        }
    }

    public async Task<SkillCheckResult> SkillCheckAsync(Guid sessionId, string skill, Guid? playerId = null, int? dc = null)
    {
        var d20Result = await RollDiceAsync(sessionId, "1d20", playerId);

        // Calculate modifier based on system (simplified: use d20 roll)
        var modifier = 0;
        if (playerId.HasValue)
        {
            var player = await _context.Players
                .Include(p => p.Character)
                .FirstOrDefaultAsync(p => p.Id == playerId.Value);

            if (player?.Character != null)
            {
                var attrVal = player.Character.Attributes;
                if (attrVal.TryGetProperty("attributes", out var attrs) && attrs.TryGetProperty(skill.ToLower(), out var stat))
                {
                    modifier = (stat.GetInt32() - 10) / 2; // D&D 5e style
                }
            }
        }

        var total = d20Result.Total + modifier;
        var rollDC = dc ?? 15; // Default DC 15
        var success = total >= rollDC;

        _logger.LogInformation("Skill check: {Skill} by player {PlayerId}: d20({Total}) + {Modifier} = {TotalWithMod} vs DC {DC} -> {Success}",
            skill, playerId, d20Result.Total, modifier, total, rollDC, success ? "SUCCESS" : "FAILURE");

        return new SkillCheckResult
        {
            Skill = skill,
            DiceRoll = d20Result.Total,
            Modifier = modifier,
            Total = total,
            DC = rollDC,
            Success = success,
            RolledAt = DateTime.UtcNow
        };
    }

    public async Task<AttackResult> AttackAsync(Guid sessionId, string weapon, string targetName, Guid? playerId = null)
    {
        // Roll attack
        var attackRoll = await RollDiceAsync(sessionId, "1d20", playerId);

        // Calculate attack modifier (simplified)
        var attackMod = 0;
        if (playerId.HasValue)
        {
            var player = await _context.Players
                .Include(p => p.Character)
                .FirstOrDefaultAsync(p => p.Id == playerId.Value);

            if (player?.Character != null)
            {
                var attrs = player.Character.Attributes;
                if (attrs.TryGetProperty("attributes", out var stats) && stats.TryGetProperty("dexterity", out var dex))
                {
                    attackMod = (dex.GetInt32() - 10) / 2;
                }
            }
        }

        var attackTotal = attackRoll.Total + attackMod;
        var ac = 15; // Default AC (can be overridden via metadata in future)
        var hit = attackTotal >= ac;

        AttackResult result;
        if (hit)
        {
            // Roll damage
            var damageRoll = await RollDiceAsync(sessionId, "1d8+1", playerId);
            result = new AttackResult
            {
                Weapon = weapon,
                Target = targetName,
                Hit = true,
                AttackRoll = attackTotal,
                AC = ac,
                DamageDice = damageRoll.Formula,
                DamageTotal = damageRoll.Total,
                RolledAt = DateTime.UtcNow
            };
        }
        else
        {
            result = new AttackResult
            {
                Weapon = weapon,
                Target = targetName,
                Hit = false,
                AttackRoll = attackTotal,
                AC = ac,
                DamageTotal = 0,
                RolledAt = DateTime.UtcNow
            };
        }

        _logger.LogInformation("Attack: {Weapon} by {PlayerId} vs {Target}: {AttackTotal} vs AC {AC} -> {Hit}",
            weapon, playerId, targetName, attackTotal, ac, hit ? "HIT" : "MISS");

        return result;
    }

    public async Task<Character> CreateCharacterAsync(Guid playerId, string systemId, JsonDocument? template = null)
    {
        var player = await _context.Players.FindAsync(playerId);
        if (player == null)
            throw new InvalidOperationException($"Player {playerId} not found.");

        var system = _systemRegistry.GetSystem(systemId);
        if (system == null)
            throw new InvalidOperationException($"Unknown system: {systemId}");

        var defaultTemplate = _systemRegistry.CreateDefaultCharacter(systemId);
        var attrs = template?.RootElement ?? defaultTemplate.RootElement;

        // Extract HP values from template if available
        int currentHP = 10, maxHP = 10;
        if (attrs.TryGetProperty("currentHP", out var currentHPProp) && currentHPProp.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            currentHP = currentHPProp.GetInt32();
        }
        if (attrs.TryGetProperty("maxHP", out var maxHPProp) && maxHPProp.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            maxHP = maxHPProp.GetInt32();
        }

        var character = new Character
        {
            PlayerId = playerId,
            Name = player.CharacterName,
            Class = attrs.TryGetProperty("class", out var classProp) && classProp.ValueKind == System.Text.Json.JsonValueKind.String
                ? classProp.GetString() ?? "Custom"
                : "Custom",
            Level = attrs.TryGetProperty("level", out var levelProp) && levelProp.ValueKind == System.Text.Json.JsonValueKind.Number
                ? levelProp.GetInt32()
                : 1,
            ProficiencyBonus = _rulesFactory.GetRules(systemId).GetProficiencyBonus(
                attrs.TryGetProperty("level", out var lvlProp) && lvlProp.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? lvlProp.GetInt32()
                    : 1),
            CurrentHP = currentHP,
            MaxHP = maxHP,
            Attributes = attrs.TryGetProperty("attributes", out var attrProp) ? attrProp : default,
            Skills = attrs.TryGetProperty("skills", out var skillProp) ? skillProp : default,
            Inventory = attrs.TryGetProperty("inventory", out var invProp) ? invProp : default,
            Spells = attrs.TryGetProperty("spells", out var spellProp) ? spellProp : default,
            Conditions = attrs.TryGetProperty("conditions", out var condProp) ? condProp : default,
            CustomFields = attrs.TryGetProperty("customFields", out var customProp) ? customProp : default
        };

        _context.Characters.Add(character);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created character '{CharacterName}' for player {PlayerId} (system: {SystemId})",
            character.Name, playerId, systemId);

        return character;
    }

    public async Task<Character> UpdateCharacterAsync(Guid characterId, JsonPatch patch)
    {
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null)
            throw new InvalidOperationException($"Character {characterId} not found.");

        // Apply patch operations
        foreach (var op in patch.Operations)
        {
            switch (op.Path)
            {
                case "name":
                    character.Name = op.Value?.ToString() ?? character.Name;
                    break;
                case "class":
                    character.Class = op.Value?.ToString() ?? character.Class;
                    break;
                case "level":
                    character.Level = op.Value?.GetInt32() ?? character.Level;
                    break;
                case "currentHP":
                    character.CurrentHP = op.Value?.GetInt32() ?? character.CurrentHP;
                    break;
                case "maxHP":
                    character.MaxHP = op.Value?.GetInt32() ?? character.MaxHP;
                    break;
                default:
                    // Handle nested paths (simplified)
                    if (op.Path.StartsWith("attributes."))
                    {
                        var attrName = op.Path.Substring("attributes.".Length);
                        var attrs = character.Attributes;
                        if (!attrs.TryGetProperty("attributes", out var attrObj))
                        {
                            attrObj = JsonDocument.Parse("{}").RootElement;
                        }
                        // Update attribute (simplified)
                        _logger.LogDebug("Updating attribute {AttrName} on character {CharacterId}", attrName, characterId);
                    }
                    break;
            }
        }

        character.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return character;
    }

    public async Task<Character?> GetCharacterAsync(Guid characterId)
    {
        return await _context.Characters.FindAsync(characterId);
    }

    public async Task<JsonDocument> GetDefaultCharacterTemplateAsync(string systemId)
    {
        return _systemRegistry.CreateDefaultCharacter(systemId);
    }

    public async Task<(bool valid, string[] errors)> ValidateCharacterAsync(Guid playerId, string systemId, JsonElement character)
    {
        // Check player ownership
        var player = await _context.Players.FindAsync(playerId);
        if (player == null)
            return (false, new[] { $"Player {playerId} not found." });

        return _systemRegistry.ValidateCharacter(systemId, character);
    }
}

/// <summary>
/// JSON Patch operations for character updates.
/// </summary>
public class JsonPatch
{
    public List<PatchOperation> Operations { get; set; } = new();
}

public class PatchOperation
{
    public string Op { get; set; } = "replace"; // add, remove, replace
    public string Path { get; set; } = string.Empty;
    public JsonElement? Value { get; set; }
}

/// <summary>
/// Results of a skill check.
/// </summary>
public class SkillCheckResult
{
    public string Skill { get; set; } = string.Empty;
    public int DiceRoll { get; set; }
    public int Modifier { get; set; }
    public int Total { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
    public DateTime RolledAt { get; set; }
}

/// <summary>
/// Results of an attack roll.
/// </summary>
public class AttackResult
{
    public string Weapon { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Hit { get; set; }
    public int AttackRoll { get; set; }
    public int AC { get; set; }
    public string? DamageDice { get; set; }
    public int DamageTotal { get; set; }
    public DateTime RolledAt { get; set; }
}
