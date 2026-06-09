using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Strategy interface for system-specific RPG rules.
/// Each RPG system (D&amp;D 5e, PF2e, CoC 7e, custom) provides its own implementation.
/// This eliminates the need for switch/if-else chains throughout the codebase.
/// </summary>
public interface ISystemRules
{
    /// <summary>
    /// Gets the system ID this strategy handles.
    /// </summary>
    string SystemId { get; }

    /// <summary>
    /// Determines if a natural 20 attack roll is a critical hit.
    /// </summary>
    bool IsCriticalHit(int attackDieRoll, string systemId);

    /// <summary>
    /// Determines if a natural 1 attack roll is a fumble.
    /// </summary>
    bool IsFumble(int attackDieRoll, string systemId);

    /// <summary>
    /// Calculates proficiency bonus for a given level.
    /// </summary>
    int GetProficiencyBonus(int level);

    /// <summary>
    /// Calculates the attribute modifier from an attribute value
    /// (e.g., (16-10)/2 = +3 for D&amp;D, (60-50)/10 = +1 for CoC).
    /// </summary>
    int GetAttributeModifier(int attributeValue);

    /// <summary>
    /// Calculates the attribute value from a modifier (e.g., 10 + 3 = 13).
    /// </summary>
    int GetAttributeValue(int modifier);

    /// <summary>
    /// Determines if a save is a critical success or critical failure.
    /// </summary>
    (bool isCritSuccess, bool isCritFail) EvaluateSaveResult(int roll, int dc, string systemId);

    /// <summary>
    /// Calculates XP needed to reach a given level.
    /// </summary>
    int GetXPForLevel(int level, string systemId);

    /// <summary>
    /// Calculates combat encounter XP reward for a participant.
    /// </summary>
    int GetCombatXPReward(int participantLevel, string systemId);

    /// <summary>
    /// Applies system-specific effects after a combat event.
    /// </summary>
    Task<Combat> ApplySystemSpecificEffectsAsync(Combat combat, string systemId, Guid participantId, AppDbContext context);

    /// <summary>
    /// Determines initial HP for a character at a given level.
    /// </summary>
    int GetInitialHP(int level, JsonElement characterData, string systemId);

    /// <summary>
    /// Processes rest effects for a participant.
    /// Returns the number of HP recovered during the rest.
    /// </summary>
    Task<int> ProcessRestAsync(Combat combat, Guid participantId, string systemId, AppDbContext context, bool isLongRest);

    /// <summary>
    /// Processes SAN loss for Call of Cthulhu.
    /// </summary>
    (int newCurrentSAN, int newMaxSAN, bool wentInsane) ProcessSANLoss(int currentSAN, int maxSAN, int sanLoss, string reason);

    /// <summary>
    /// Processes SAN recovery.
    /// </summary>
    (int newCurrentSAN, int newMaxSAN) ProcessSANRecovery(int currentSAN, int maxSAN, int sanRecovery);

    /// <summary>
    /// Performs a SAN check for Call of Cthulhu.
    /// </summary>
    Task<SanityCheckResult> PerformSANCheckAsync(Combat combat, Guid participantId, int dc, AppDbContext context);
}

/// <summary>
/// Factory for creating system-specific rules strategies.
/// Uses a registry pattern to look up the correct strategy by system ID.
/// </summary>
public interface ISystemRulesFactory
{
    /// <summary>
    /// Gets the system rules strategy for the given system ID.
    /// Returns a default (D&amp;D 5e) strategy if none is registered.
    /// </summary>
    ISystemRules GetRules(string systemId);

    /// <summary>
    /// Registers a custom system rules implementation at runtime.
    /// </summary>
    void RegisterRules(ISystemRules rules);

    /// <summary>
    /// Checks if a system has custom rules registered.
    /// </summary>
    bool HasRules(string systemId);
}

// ============================================================================
// Concrete Strategy: D&D 5e Rules
// ============================================================================

/// <summary>
/// Concrete strategy for D&amp;D 5e rules.
/// </summary>
public class DnD5eRules : ISystemRules
{
    public string SystemId => "dnd5e";

    public bool IsCriticalHit(int attackDieRoll, string systemId) => attackDieRoll == 20;
    public bool IsFumble(int attackDieRoll, string systemId) => attackDieRoll == 1;

    public int GetProficiencyBonus(int level)
    {
        if (level <= 4) return 2;
        if (level <= 8) return 3;
        if (level <= 12) return 4;
        if (level <= 16) return 5;
        return 6;
    }

    public int GetAttributeModifier(int attributeValue) => (attributeValue - 10) / 2;
    public int GetAttributeValue(int modifier) => 10 + modifier;

    public (bool isCritSuccess, bool isCritFail) EvaluateSaveResult(int roll, int dc, string systemId)
    {
        // Natural 20 always succeeds, natural 1 always fails
        return (roll == 20, roll == 1);
    }

    public int GetXPForLevel(int level, string systemId)
    {
        return level switch
        {
            1 => 0, 2 => 300, 3 => 900, 4 => 2700, 5 => 6500,
            6 => 14000, 7 => 23000, 8 => 34000, 9 => 48000, 10 => 64000,
            11 => 85000, 12 => 100000, 13 => 120000, 14 => 140000, 15 => 165000,
            16 => 195000, 17 => 225000, 18 => 265000, 19 => 305000, 20 => 355000,
            _ => throw new ArgumentException($"Unknown level: {level}")
        };
    }

    public int GetCombatXPReward(int participantLevel, string systemId)
    {
        var xpByCR = new Dictionary<int, int>
        {
            [0] = 0, [1] = 25, [2] = 50, [3] = 70, [4] = 100,
            [5] = 150, [6] = 200, [7] = 250, [8] = 300, [9] = 450,
            [10] = 600, [11] = 750, [12] = 1000, [13] = 1200, [14] = 1600,
            [15] = 2000, [16] = 2500, [17] = 3000, [18] = 4500, [19] = 5500, [20] = 6000
        };
        return xpByCR.GetValueOrDefault(participantLevel, 0);
    }

    public Task<Combat> ApplySystemSpecificEffectsAsync(Combat combat, string systemId, Guid participantId, AppDbContext context)
    {
        // D&D 5e: short rest HP recovery (1d6 per level), long rest full restore
        return Task.FromResult(combat);
    }

    public int GetInitialHP(int level, JsonElement characterData, string systemId)
    {
        // Default: 10 + con mod per level (simplified)
        var conMod = 0;
        if (characterData.TryGetProperty("attributes", out var attrs) &&
            attrs.TryGetProperty("constitution", out var con))
        {
            conMod = (con.GetInt32() - 10) / 2;
        }
        return 10 + conMod + ((level - 1) * (conMod >= 0 ? conMod : conMod - 1));
    }

    public async Task<int> ProcessRestAsync(Combat combat, Guid participantId, string systemId, AppDbContext context, bool isLongRest)
    {
        if (isLongRest)
        {
            // Long rest: full recovery handled by caller
            return 0;
        }
        // Short rest: 1d6 HP per level
        return new Random().Next(1, 7);
    }

    public (int newCurrentSAN, int newMaxSAN, bool wentInsane) ProcessSANLoss(int currentSAN, int maxSAN, int sanLoss, string reason)
    {
        var newCurrentSAN = Math.Max(0, currentSAN - sanLoss);
        var wentInsane = newCurrentSAN == 0;
        return (newCurrentSAN, maxSAN, wentInsane);
    }

    public (int newCurrentSAN, int newMaxSAN) ProcessSANRecovery(int currentSAN, int maxSAN, int sanRecovery)
    {
        var newCurrentSAN = Math.Min(maxSAN, currentSAN + sanRecovery);
        return (newCurrentSAN, maxSAN);
    }

    public Task<SanityCheckResult> PerformSANCheckAsync(Combat combat, Guid participantId, int dc, AppDbContext context)
    {
        // CoC-specific, but included for interface compliance
        return Task.FromResult(new SanityCheckResult { Success = true, Effect = "N/A for this system" });
    }
}

// ============================================================================
// Concrete Strategy: Pathfinder 2e Rules
// ============================================================================

/// <summary>
/// Concrete strategy for Pathfinder 2e rules.
/// </summary>
public class PF2eRules : ISystemRules
{
    public string SystemId => "pf2e";

    public bool IsCriticalHit(int attackDieRoll, string systemId) => attackDieRoll == 20;
    public bool IsFumble(int attackDieRoll, string systemId) => attackDieRoll == 1;

    public int GetProficiencyBonus(int level)
    {
        // PF2e proficiency scales with level: untrained(0), trained(2), expert(4), master(6), legendary(8)
        return level / 2 + 1;
    }

    public int GetAttributeModifier(int attributeValue) => (attributeValue - 10) / 2;
    public int GetAttributeValue(int modifier) => 10 + modifier;

    public (bool isCritSuccess, bool isCritFail) EvaluateSaveResult(int roll, int dc, string systemId)
    {
        return (roll == 20, roll == 1);
    }

    public int GetXPForLevel(int level, string systemId)
    {
        return level switch
        {
            1 => 0, 2 => 200, 3 => 600, 4 => 1300, 5 => 2500,
            6 => 4500, 7 => 7000, 8 => 11000, 9 => 15000, 10 => 22000,
            11 => 30000, 12 => 40000, 13 => 55000, 14 => 70000, 15 => 90000,
            16 => 115000, 17 => 140000, 18 => 175000, 19 => 210000, 20 => 250000,
            _ => throw new ArgumentException($"Unknown level: {level}")
        };
    }

    public int GetCombatXPReward(int participantLevel, string systemId)
    {
        return participantLevel switch
        {
            <= 4 => participantLevel * 50,
            <= 8 => participantLevel * 75,
            <= 12 => participantLevel * 100,
            <= 16 => participantLevel * 150,
            _ => participantLevel * 200
        };
    }

    public Task<Combat> ApplySystemSpecificEffectsAsync(Combat combat, string systemId, Guid participantId, AppDbContext context)
    {
        // PF2e: stamina points, recovery actions
        return Task.FromResult(combat);
    }

    public int GetInitialHP(int level, JsonElement characterData, string systemId)
    {
        // PF2e: class HP + con mod, with hit points at each level
        var conMod = 0;
        if (characterData.TryGetProperty("attributes", out var attrs) &&
            attrs.TryGetProperty("constitution", out var con))
        {
            conMod = (con.GetInt32() - 10) / 2;
        }
        return 12 + conMod + ((level - 1) * (conMod >= 0 ? conMod : conMod - 1));
    }

    public async Task<int> ProcessRestAsync(Combat combat, Guid participantId, string systemId, AppDbContext context, bool isLongRest)
    {
        if (isLongRest)
        {
            // PF2e long rest: recover stamina points
            return 0;
        }
        // PF2e short rest: recover some stamina
        return 1;
    }

    public (int newCurrentSAN, int newMaxSAN, bool wentInsane) ProcessSANLoss(int currentSAN, int maxSAN, int sanLoss, string reason)
    {
        return (currentSAN, maxSAN, false); // No SAN in PF2e
    }

    public (int newCurrentSAN, int newMaxSAN) ProcessSANRecovery(int currentSAN, int maxSAN, int sanRecovery)
    {
        return (currentSAN, maxSAN); // No SAN in PF2e
    }

    public Task<SanityCheckResult> PerformSANCheckAsync(Combat combat, Guid participantId, int dc, AppDbContext context)
    {
        return Task.FromResult(new SanityCheckResult { Success = true, Effect = "N/A for this system" });
    }
}

// ============================================================================
// Concrete Strategy: Call of Cthulhu 7e Rules
// ============================================================================

/// <summary>
/// Concrete strategy for Call of Cthulhu 7e rules.
/// </summary>
public class CoC7eRules : ISystemRules
{
    public string SystemId => "coc7e";

    public bool IsCriticalHit(int attackDieRoll, string systemId)
    {
        // CoC: roll under skill, crit at 1/5 of skill
        return attackDieRoll <= 5;
    }

    public bool IsFumble(int attackDieRoll, string systemId) => attackDieRoll == 100;

    public int GetProficiencyBonus(int level) => 0; // CoC doesn't use proficiency bonuses

    public int GetAttributeModifier(int attributeValue) => (attributeValue - 50) / 10;
    public int GetAttributeValue(int modifier) => 50 + (modifier * 10);

    public (bool isCritSuccess, bool isCritFail) EvaluateSaveResult(int roll, int dc, string systemId)
    {
        // CoC: crit success at <= 1/5 of skill, crit fail at 96-99 or natural 100
        return (roll <= dc / 5, roll >= 96);
    }

    public int GetXPForLevel(int level, string systemId) => level * 100; // Skill point based

    public int GetCombatXPReward(int participantLevel, string systemId) => participantLevel * 10;

    public Task<Combat> ApplySystemSpecificEffectsAsync(Combat combat, string systemId, Guid participantId, AppDbContext context)
    {
        // CoC: sanity effects, madness
        return Task.FromResult(combat);
    }

    public int GetInitialHP(int level, JsonElement characterData, string systemId)
    {
        // CoC: (CON + SIZ) / 10 per level
        int con = 50;
        int siz = 50;
        if (characterData.TryGetProperty("attributes", out var attrs))
        {
            if (attrs.TryGetProperty("constitution", out var conProp) && conProp.ValueKind == JsonValueKind.Number)
                con = conProp.GetInt32();
            if (attrs.TryGetProperty("size", out var sizProp) && sizProp.ValueKind == JsonValueKind.Number)
                siz = sizProp.GetInt32();
        }
        return ((con + siz) / 10) * level;
    }

    public async Task<int> ProcessRestAsync(Combat combat, Guid participantId, string systemId, AppDbContext context, bool isLongRest)
    {
        var participant = await context.CombatParticipants.FindAsync(participantId);
        if (participant == null)
            return 0;

        if (isLongRest)
        {
            // CoC long rest: recover 1d4 SAN
            var sanRecovery = new Random().Next(1, 5);
            return sanRecovery;
        }
        // CoC short rest: no SAN recovery
        return 0;
    }

    public (int newCurrentSAN, int newMaxSAN, bool wentInsane) ProcessSANLoss(int currentSAN, int maxSAN, int sanLoss, string reason)
    {
        var newCurrentSAN = Math.Max(0, currentSAN - sanLoss);
        var wentInsane = newCurrentSAN == 0;

        // Permanent SAN loss: reduce max SAN by 1/2 the loss
        var newMaxSAN = maxSAN - (sanLoss / 2);

        return (newCurrentSAN, newMaxSAN, wentInsane);
    }

    public (int newCurrentSAN, int newMaxSAN) ProcessSANRecovery(int currentSAN, int maxSAN, int sanRecovery)
    {
        var newCurrentSAN = Math.Min(maxSAN, currentSAN + sanRecovery);
        return (newCurrentSAN, maxSAN);
    }

    public async Task<SanityCheckResult> PerformSANCheckAsync(Combat combat, Guid participantId, int dc, AppDbContext context)
    {
        var participant = await context.CombatParticipants.FindAsync(participantId);
        if (participant == null)
            return new SanityCheckResult { Success = false, Effect = "Participant not found." };

        // SAN is stored in Notes JSON
        var notes = participant.Notes?.GetString() ?? "{}";
        var notesDoc = System.Text.Json.JsonDocument.Parse(notes);
        var san = 60; // default
        if (notesDoc.RootElement.TryGetProperty("currentSAN", out var sanProp) && sanProp.ValueKind == System.Text.Json.JsonValueKind.Number)
            san = sanProp.GetInt32();
        var roll = new Random().Next(1, 101);
        var success = roll <= san;
        var isCritical = roll <= (san / 5);

        int sanLoss = 0;
        string effect = string.Empty;

        if (!success)
        {
            sanLoss = roll > san * 5 ? new Random().Next(1, 5) : new Random().Next(1, 9);
            effect = $"Failed - lost {sanLoss} SAN";
        }
        else if (isCritical)
        {
            effect = "Critical success - no SAN loss";
        }
        else
        {
            effect = "Success - no SAN loss";
        }

        return new SanityCheckResult
        {
            Participant = participant.DisplayName,
            CurrentSAN = san,
            Roll = roll,
            DC = dc,
            Success = success,
            IsCritical = isCritical,
            SANLoss = sanLoss,
            Effect = effect,
            RolledAt = DateTime.UtcNow
        };
    }
}

// ============================================================================
// Factory Implementation
// ============================================================================

/// <summary>
/// Factory implementation for system-specific rules strategies.
/// Pre-registers built-in systems and allows runtime registration of custom systems.
/// </summary>
public class SystemRulesFactory : ISystemRulesFactory
{
    private readonly Dictionary<string, ISystemRules> _rules = new();

    public SystemRulesFactory()
    {
        // Register built-in systems
        RegisterRules(new DnD5eRules());
        RegisterRules(new PF2eRules());
        RegisterRules(new CoC7eRules());
    }

    public ISystemRules GetRules(string systemId)
    {
        if (!_rules.TryGetValue(systemId, out var rules))
        {
            // Fallback: return D&D 5e rules as default
            return _rules["dnd5e"];
        }
        return rules;
    }

    public void RegisterRules(ISystemRules rules)
    {
        _rules[rules.SystemId] = rules;
    }

    public bool HasRules(string systemId) => _rules.ContainsKey(systemId);
}
