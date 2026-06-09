using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for spell casting.
/// </summary>
public interface ICombatSpellService
{
    Task<SpellCastResult> CastSpellAsync(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null);
    Task<SpellCastResult> CastAreaSpellAsync(Guid combatId, string casterName, string spellName,
        string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null, Guid[]? targetIds = null);
}

public class CombatSpellService : ICombatSpellService
{
    private readonly AppDbContext _context;
    private readonly IDiceEngine _diceEngine;
    private readonly ILogger<CombatSpellService> _logger;

    public CombatSpellService(AppDbContext context, IDiceEngine diceEngine, ILogger<CombatSpellService> logger)
    {
        _context = context;
        _diceEngine = diceEngine;
        _logger = logger;
    }

    public async Task<SpellCastResult> CastSpellAsync(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var target = combat.Participants.FirstOrDefault(p => p.Id == targetId)
            ?? throw new KeyNotFoundException($"Target participant {targetId} not found in combat.");

        var saveResult = _diceEngine.Roll(saveFormula);
        var saveTotal = saveResult.Total;
        var saveSuccess = saveTotal >= saveDC;

        var isCritSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 20;
        if (isCritSave) saveSuccess = true;

        var isFumbleSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 1;
        if (isFumbleSave) saveSuccess = false;

        int damageTotal = 0;
        string damageInfo = string.Empty;
        string effect = string.Empty;

        if (damageFormula != null && !saveSuccess)
        {
            var dmgResult = _diceEngine.Roll(damageFormula);
            damageTotal = dmgResult.Total + (damageBonus ?? 0);
            damageInfo = $"{damageFormula} + {damageBonus ?? 0} = {damageTotal}";
            await DealDamageToParticipant(combat, target, damageTotal, casterName);
        }
        else if (damageFormula != null && saveSuccess)
        {
            var dmgResult = _diceEngine.Roll(damageFormula);
            damageTotal = (dmgResult.Total + (damageBonus ?? 0)) / 2;
            damageInfo = $"Half: {damageFormula} + {damageBonus ?? 0} = {damageTotal} (half)";
            await DealDamageToParticipant(combat, target, damageTotal, casterName);
        }
        else if (description != null)
        {
            effect = description;
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, casterName, target.DisplayName,
            $"Cast {spellName}: {saveFormula}={saveTotal} vs DC {saveDC} -> {(saveSuccess ? "SUCCESS" : "FAILURE")}"
            + (damageTotal > 0 ? $" | {damageInfo}" : $" | {effect}"));

        return new SpellCastResult
        {
            Caster = casterName,
            SpellName = spellName,
            Target = target.DisplayName,
            SaveType = saveFormula,
            SaveDC = saveDC,
            SaveSuccess = saveSuccess,
            IsCritical = isCritSave,
            DamageTotal = damageTotal,
            DamageInfo = damageInfo,
            Effect = effect,
            TargetHP = target.CurrentHP,
            TargetMaxHP = target.MaxHP,
            RolledAt = DateTime.UtcNow
        };
    }

    public async Task<SpellCastResult> CastAreaSpellAsync(Guid combatId, string casterName, string spellName,
        string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null, Guid[]? targetIds = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var targets = targetIds != null
            ? combat.Participants.Where(p => targetIds.Contains(p.Id)).ToList()
            : combat.Participants.Where(p => p.CurrentHP > 0).ToList();

        if (!targets.Any())
            throw new InvalidOperationException("No valid targets for area spell.");

        int totalDamage = 0;
        var results = new List<string>();

        foreach (var target in targets)
        {
            var saveResult = _diceEngine.Roll(saveFormula);
            var saveTotal = saveResult.Total;
            var saveSuccess = saveTotal >= saveDC;

            int dmg = 0;
            if (damageFormula != null && !saveSuccess)
            {
                var dmgResult = _diceEngine.Roll(damageFormula);
                dmg = dmgResult.Total + (damageBonus ?? 0);
                await DealDamageToParticipant(combat, target, dmg, casterName);
                totalDamage += dmg;
            }
            else if (damageFormula != null && saveSuccess)
            {
                var dmgResult = _diceEngine.Roll(damageFormula);
                dmg = (dmgResult.Total + (damageBonus ?? 0)) / 2;
                await DealDamageToParticipant(combat, target, dmg, casterName);
                totalDamage += dmg;
            }

            var dmgStr = dmg > 0 ? $" ({dmg} dmg)" : "";
            results.Add($"{target.DisplayName}: {saveFormula}={saveTotal} vs DC {saveDC} -> {(saveSuccess ? "SUCCESS" : "FAILURE")}{dmgStr}");
        }

        var effect = description ?? $"{targets.Count} target(s) affected, {totalDamage} total damage";

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, casterName, "Area",
            $"Cast {spellName} (AoE): {string.Join(", ", results)} | {effect}");

        return new SpellCastResult
        {
            Caster = casterName,
            SpellName = spellName,
            Target = $"{targets.Count} targets",
            SaveType = saveFormula,
            SaveDC = saveDC,
            SaveSuccess = true,
            DamageTotal = totalDamage,
            DamageInfo = effect,
            Effect = $"Area of Effect: {targets.Count} targets",
            RolledAt = DateTime.UtcNow
        };
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    private async Task DealDamageToParticipant(Combat combat, CombatParticipant participant, int damage, string source)
    {
        var tempHP = participant.TemporaryHP?.GetInt32() ?? 0;
        int damageToHP = damage;

        if (tempHP > 0)
        {
            if (damage <= tempHP)
            {
                participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Damage, source, participant.DisplayName,
                    $"Took {damage} damage (absorbed by {damage} temp HP).");
                return;
            }
            else
            {
                damageToHP = damage - tempHP;
                participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
            }
        }

        participant.CurrentHP = Math.Max(0, participant.CurrentHP - damageToHP);

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Damage, source, participant.DisplayName,
            $"Took {damageToHP} damage ({participant.CurrentHP}/{participant.MaxHP} HP remaining).");

        if (participant.CurrentHP <= 0)
        {
            var ds = GetDeathSaveState(participant);
            if (ds.Successes == 0 && ds.Failures == 0)
            {
                participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(new DeathSaveState { Successes = 0, Failures = 0 })).RootElement;
            }
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
    }

    private DeathSaveState GetDeathSaveState(CombatParticipant participant)
    {
        if (participant.DeathSaveState.HasValue &&
            participant.DeathSaveState.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<DeathSaveState>(participant.DeathSaveState.ToString())
                ?? new DeathSaveState();
        }
        return new DeathSaveState();
    }

    private async Task AddCombatEvent(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        _context.CombatEvents.Add(evt);
        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Service for inventory/equipment management.
/// </summary>
public interface ICombatInventoryService
{
    Task<Combat> AddItemToParticipantAsync(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null);
    Task<Combat> RemoveItemFromParticipantAsync(Guid combatId, Guid participantId, string itemName);
    Task<Combat> EquipItemAsync(Guid combatId, Guid participantId, string itemName);
    Task<Combat> UnequipItemAsync(Guid combatId, Guid participantId, string itemName);
}

public class CombatInventoryService : ICombatInventoryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CombatInventoryService> _logger;

    public CombatInventoryService(AppDbContext context, ILogger<CombatInventoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Combat> AddItemToParticipantAsync(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("inventory")) notes["inventory"] = new List<object>();

        var invList = JsonSerializer.Deserialize<List<object>>(notes["inventory"]?.ToString() ?? "[]") ?? new List<object>();
        invList.Add(new { Name = itemName, Type = itemType, Quantity = quantity, Stats = itemStats });
        notes["inventory"] = invList;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Added {quantity}x {itemName} ({itemType}).");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> RemoveItemFromParticipantAsync(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes != null && notes.ContainsKey("inventory"))
        {
            var invList = JsonSerializer.Deserialize<List<object>>(notes["inventory"]?.ToString() ?? "[]") ?? new List<object>();
            invList.RemoveAll(item =>
            {
                var obj = item as Dictionary<string, object>;
                return obj != null && obj.ContainsKey("Name") && obj["Name"].ToString() == itemName;
            });
            notes["inventory"] = invList;
            participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Removed {itemName}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> EquipItemAsync(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Equipped {itemName}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> UnequipItemAsync(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Unequipped {itemName}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    private Dictionary<string, object>? GetNotes(CombatParticipant participant)
    {
        if (participant.Notes.HasValue &&
            participant.Notes.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(participant.Notes.ToString());
        }
        return null;
    }

    private async Task AddCombatEvent(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        _context.CombatEvents.Add(evt);
        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Service for character progression (XP, leveling).
/// Uses ISystemRules for system-specific calculations.
/// </summary>
public interface ICombatProgressionService
{
    Task<Combat> AddXPAsync(Guid combatId, Guid participantId, int xpAmount, string reason);
    Task<Combat> LevelUpAsync(Guid combatId, Guid participantId, int newLevel, string systemId);
    Task<Combat> CalculateXPForCombatAsync(Guid combatId, string systemId);
}

public class CombatProgressionService : ICombatProgressionService
{
    private readonly AppDbContext _context;
    private readonly ISystemRulesFactory _rulesFactory;
    private readonly ILogger<CombatProgressionService> _logger;

    public CombatProgressionService(AppDbContext context, ISystemRulesFactory rulesFactory,
        ILogger<CombatProgressionService> logger)
    {
        _context = context;
        _rulesFactory = rulesFactory;
        _logger = logger;
    }

    public async Task<Combat> AddXPAsync(Guid combatId, Guid participantId, int xpAmount, string reason)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("xp")) notes["xp"] = 0;
        notes["xp"] = (int)notes["xp"] + xpAmount;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Revival, participant.DisplayName, participant.DisplayName,
            $"Gained {xpAmount} XP ({reason}). Total: {notes["xp"]}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> LevelUpAsync(Guid combatId, Guid participantId, int newLevel, string systemId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var oldLevel = participant.MaxHP;
        participant.MaxHP = newLevel;

        // Use ISystemRules for system-specific HP calculation
        var rules = _rulesFactory.GetRules(systemId);
        var hpPerLevel = rules.GetAttributeModifier(16); // Simplified: use CON mod as HP per level
        var newMaxHP = participant.CurrentHP + Math.Max(hpPerLevel, 1);

        participant.CurrentHP = newMaxHP;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Revival, participant.DisplayName, participant.DisplayName,
            $"Levelled up! {oldLevel} -> {newLevel}. HP: {participant.CurrentHP}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> CalculateXPForCombatAsync(Guid combatId, string systemId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var defeatedNPCs = combat.Participants.Where(p =>
            p.ParticipantType == "NPC" && p.CurrentHP <= 0).ToList();

        int totalXP = 0;
        var rules = _rulesFactory.GetRules(systemId);

        foreach (var npc in defeatedNPCs)
        {
            var xp = rules.GetCombatXPReward(1, systemId); // Simplified: assume level 1 NPC
            totalXP += xp;

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Revival, "System", npc.DisplayName,
                $"Defeated NPC awarded {xp} XP.");
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            $"Total XP from combat: {totalXP} ({defeatedNPCs.Count} NPCs)");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    private Dictionary<string, object>? GetNotes(CombatParticipant participant)
    {
        if (participant.Notes.HasValue &&
            participant.Notes.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(participant.Notes.ToString());
        }
        return null;
    }

    private async Task AddCombatEvent(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        _context.CombatEvents.Add(evt);
        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Service for grid/map management.
/// </summary>
public interface ICombatGridService
{
    Task<Combat> SetGridSizeAsync(Guid combatId, int width, int height);
    Task<Combat> SetParticipantPositionAsync(Guid combatId, Guid participantId, int gridX, int gridY);
    Task<Combat> MoveParticipantAsync(Guid combatId, Guid participantId, int newGridX, int newGridY);
    Task<GridPosition?> GetParticipantPositionAsync(Guid combatId, Guid participantId);
    Task<List<GridPosition>> GetAdjacentPositionsAsync(Guid combatId, int gridX, int gridY, int range = 1);
}

public class CombatGridService : ICombatGridService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CombatGridService> _logger;

    public CombatGridService(AppDbContext context, ILogger<CombatGridService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Combat> SetGridSizeAsync(Guid combatId, int width, int height)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var notes = new Dictionary<string, object>
        {
            { "gridWidth", width },
            { "gridHeight", height }
        };
        combat.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            $"Grid size set to {width}x{height}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> SetParticipantPositionAsync(Guid combatId, Guid participantId, int gridX, int gridY)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();
        notes["gridX"] = gridX;
        notes["gridY"] = gridY;
        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Moved to grid position ({gridX}, {gridY}).");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> MoveParticipantAsync(Guid combatId, Guid participantId, int newGridX, int newGridY)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        var moveSpeed = 30;
        if (notes != null && notes.ContainsKey("moveSpeed"))
        {
            moveSpeed = Convert.ToInt32(notes["moveSpeed"]);
        }

        var currentX = notes != null && notes.ContainsKey("gridX") ? Convert.ToInt32(notes["gridX"]) : 0;
        var currentY = notes != null && notes.ContainsKey("gridY") ? Convert.ToInt32(notes["gridY"]) : 0;
        var distance = Math.Abs(newGridX - currentX) + Math.Abs(newGridY - currentY);
        var squaresMoved = (distance + 4) / 5;
        var movementCost = squaresMoved * 5;

        if (movementCost > moveSpeed)
        {
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Cannot move to ({newGridX}, {newGridY}). Requires {movementCost} ft, has {moveSpeed} ft.");
            return combat;
        }

        await SetParticipantPositionAsync(combatId, participantId, newGridX, newGridY);
        return combat;
    }

    public async Task<GridPosition?> GetParticipantPositionAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null) return null;

        var notes = GetNotes(participant);
        if (notes == null) return null;

        return new GridPosition
        {
            ParticipantId = participantId,
            GridX = notes.ContainsKey("gridX") ? Convert.ToInt32(notes["gridX"]) : 0,
            GridY = notes.ContainsKey("gridY") ? Convert.ToInt32(notes["gridY"]) : 0,
            DisplayName = participant.DisplayName,
            MoveSpeed = notes.ContainsKey("moveSpeed") ? Convert.ToInt32(notes["moveSpeed"]) : 30
        };
    }

    public Task<List<GridPosition>> GetAdjacentPositionsAsync(Guid combatId, int gridX, int gridY, int range = 1)
    {
        var positions = new List<GridPosition>();

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Math.Abs(dx) + Math.Abs(dy) > range) continue;

                positions.Add(new GridPosition
                {
                    GridX = gridX + dx,
                    GridY = gridY + dy
                });
            }
        }

        return Task.FromResult(positions);
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    private Dictionary<string, object>? GetNotes(CombatParticipant participant)
    {
        if (participant.Notes.HasValue &&
            participant.Notes.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(participant.Notes.ToString());
        }
        return null;
    }

    private async Task AddCombatEvent(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        _context.CombatEvents.Add(evt);
        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Service for AI combat analysis.
/// </summary>
public interface ICombatAIService
{
    Task<AICombatSuggestion> GetAITacticalSuggestionsAsync(Guid combatId);
    Task<AICombatSuggestion> GetAINPCBehaviorAsync(Guid combatId, Guid npcId);
    Task<Combat> AutoResolveCombatAsync(Guid combatId, string resolutionMode = "quick");
}

public class CombatAIService : ICombatAIService
{
    private readonly AppDbContext _context;
    private readonly IDiceEngine _diceEngine;
    private readonly ILogger<CombatAIService> _logger;

    public CombatAIService(AppDbContext context, IDiceEngine diceEngine, ILogger<CombatAIService> logger)
    {
        _context = context;
        _diceEngine = diceEngine;
        _logger = logger;
    }

    public async Task<AICombatSuggestion> GetAITacticalSuggestionsAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var suggestions = new AICombatSuggestion();

        var livingParticipants = combat.Participants.Where(p => p.CurrentHP > 0).ToList();
        var deadParticipants = combat.Participants.Where(p => p.CurrentHP <= 0).ToList();
        var lowHPParticipants = livingParticipants.Where(p => p.CurrentHP < p.MaxHP * 0.3).ToList();
        var highThreatNPCs = combat.Participants.Where(p =>
            p.ParticipantType == "NPC" && p.CurrentHP > p.MaxHP * 0.5).ToList();

        int npcCount = combat.Participants.Count(p => p.ParticipantType == "NPC" && p.CurrentHP > 0);
        int playerCount = combat.Participants.Count(p => p.ParticipantType == "Player" && p.CurrentHP > 0);

        if (npcCount >= playerCount * 2)
            suggestions.ThreatLevel = "Extreme";
        else if (npcCount >= playerCount)
            suggestions.ThreatLevel = "High";
        else if (npcCount > playerCount / 2)
            suggestions.ThreatLevel = "Medium";
        else
            suggestions.ThreatLevel = "Low";

        foreach (var player in livingParticipants.Where(p => p.ParticipantType == "Player"))
        {
            var lowestHPEnemy = highThreatNPCs.OrderBy(p => p.CurrentHP).FirstOrDefault();
            if (lowestHPEnemy != null)
            {
                suggestions.Suggestions.Add(new AITacticalAction
                {
                    Actor = player.DisplayName,
                    Action = "Attack",
                    Target = lowestHPEnemy.DisplayName,
                    Reason = $"Lowest HP target ({lowestHPEnemy.CurrentHP}/{lowestHPEnemy.MaxHP} HP)",
                    Priority = 1
                });
            }

            if (lowHPParticipants.Contains(player))
            {
                suggestions.Suggestions.Add(new AITacticalAction
                {
                    Actor = player.DisplayName,
                    Action = "Use Healing",
                    Target = player.DisplayName,
                    Reason = $"Low HP ({player.CurrentHP}/{player.MaxHP}) - needs healing",
                    Priority = 2
                });
            }

            suggestions.Suggestions.Add(new AITacticalAction
            {
                Actor = player.DisplayName,
                Action = "Use Ability/Spell",
                Target = "",
                Reason = "Consider using class abilities or spells",
                Priority = 3
            });
        }

        foreach (var npc in highThreatNPCs)
        {
            var closestPlayer = livingParticipants
                .Where(p => p.ParticipantType == "Player")
                .FirstOrDefault();

            if (closestPlayer != null)
            {
                suggestions.NPCActions.Add(new AINPCAction
                {
                    NPCName = npc.DisplayName,
                    Behavior = npc.CurrentHP < npc.MaxHP * 0.3 ? "Fleeing" : "Aggressive",
                    Target = closestPlayer.DisplayName,
                    Action = npc.CurrentHP < npc.MaxHP * 0.3 ? "Retreat" : "Attack",
                    Reason = npc.CurrentHP < npc.MaxHP * 0.3
                        ? "Low HP - should flee"
                        : "Primary target available"
                });
            }
        }

        if (lowHPParticipants.Any())
        {
            foreach (var lowHP in lowHPParticipants)
            {
                if (lowHP.CurrentHP < lowHP.MaxHP * 0.2)
                {
                    suggestions.Warnings.Add(new AICombatWarning
                    {
                        Message = $"{lowHP.DisplayName} is critically low on HP ({lowHP.CurrentHP}/{lowHP.MaxHP})",
                        Severity = "High",
                        AffectedParticipant = lowHP.DisplayName
                    });
                }
                else
                {
                    suggestions.Warnings.Add(new AICombatWarning
                    {
                        Message = $"{lowHP.DisplayName} is low on HP ({lowHP.CurrentHP}/{lowHP.MaxHP})",
                        Severity = "Medium",
                        AffectedParticipant = lowHP.DisplayName
                    });
                }
            }
        }

        if (deadParticipants.Any(p => p.ParticipantType == "Player"))
        {
            foreach (var dead in deadParticipants.Where(p => p.ParticipantType == "Player"))
            {
                suggestions.Warnings.Add(new AICombatWarning
                {
                    Message = $"{dead.DisplayName} is down and needs attention!",
                    Severity = "High",
                    AffectedParticipant = dead.DisplayName
                });
            }
        }

        if (suggestions.ThreatLevel == "Extreme")
            suggestions.RecommendedStrategy = "Consider retreating or using powerful abilities. Focus on healing and defense.";
        else if (suggestions.ThreatLevel == "High")
            suggestions.RecommendedStrategy = "Stay alert. Prioritize healing low HP allies and focus fire on weak enemies.";
        else if (suggestions.ThreatLevel == "Medium")
            suggestions.RecommendedStrategy = "Balanced approach. Use abilities wisely and maintain formation.";
        else
            suggestions.RecommendedStrategy = "Victory seems likely. Push forward but stay cautious.";

        return suggestions;
    }

    public async Task<AICombatSuggestion> GetAINPCBehaviorAsync(Guid combatId, Guid npcId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var npc = combat.Participants.FirstOrDefault(p => p.Id == npcId)
            ?? throw new KeyNotFoundException($"NPC {npcId} not found in combat.");

        var suggestions = new AICombatSuggestion();

        string behavior, targetAction, reason;

        if (npc.CurrentHP <= 0)
        {
            behavior = "Dead"; targetAction = "None"; reason = "NPC is downed";
        }
        else if (npc.CurrentHP < npc.MaxHP * 0.3)
        {
            behavior = "Fleeing"; targetAction = "Retreat"; reason = "Low HP - should flee from combat";
        }
        else if (npc.CurrentHP < npc.MaxHP * 0.6)
        {
            behavior = "Cautious"; targetAction = "Defensive"; reason = "Moderate damage - use caution";
        }
        else
        {
            behavior = "Aggressive"; targetAction = "Attack"; reason = "Full HP - aggressive stance";
        }

        var livingPlayers = combat.Participants
            .Where(p => p.ParticipantType == "Player" && p.CurrentHP > 0)
            .ToList();

        string target = livingPlayers.Any() ? livingPlayers.First().DisplayName : "None";

        suggestions.NPCActions.Add(new AINPCAction
        {
            NPCName = npc.DisplayName,
            Behavior = behavior,
            Target = target,
            Action = targetAction,
            Reason = reason
        });

        suggestions.ThreatLevel = npc.CurrentHP >= npc.MaxHP * 0.6 ? "High" : "Medium";
        return suggestions;
    }

    public async Task<Combat> AutoResolveCombatAsync(Guid combatId, string resolutionMode = "quick")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var players = combat.Participants.Where(p => p.ParticipantType == "Player" && p.CurrentHP > 0).ToList();
        var npcs = combat.Participants.Where(p => p.ParticipantType == "NPC" && p.CurrentHP > 0).ToList();

        if (resolutionMode == "quick")
        {
            int totalPlayerHP = players.Sum(p => p.CurrentHP);
            int totalNPCsHP = npcs.Sum(p => p.CurrentHP);
            int totalPlayerInit = players.Sum(p => p.Initiative);
            int totalNPCsInit = npcs.Sum(p => p.Initiative);

            string result;
            if (totalPlayerHP > totalNPCsHP && totalPlayerInit > totalNPCsInit)
            {
                result = "Players win by overwhelming force";
                foreach (var npc in npcs)
                {
                    npc.CurrentHP = 0;
                    await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                        CombatEventType.Death, "System", npc.DisplayName,
                        "Defeated in quick resolution.");
                }
            }
            else if (totalNPCsHP > totalPlayerHP * 1.5)
            {
                result = "NPCs win by overwhelming force";
                foreach (var player in players)
                {
                    player.CurrentHP = 0;
                    await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                        CombatEventType.Death, "System", player.DisplayName,
                        "Defeated in quick resolution.");
                }
            }
            else
            {
                result = "Combat is evenly matched - GM should resolve manually";
            }

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.CombatEnd, "System", "System",
                $"Quick resolution: {result}");
        }
        else
        {
            for (int round = 0; round < 3; round++)
            {
                foreach (var player in players.Where(p => p.CurrentHP > 0))
                {
                    var target = npcs.FirstOrDefault(n => n.CurrentHP > 0);
                    if (target != null)
                    {
                        var dmg = _diceEngine.Roll("2d6");
                        target.CurrentHP = Math.Max(0, target.CurrentHP - dmg.Total);
                        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                            CombatEventType.Damage, player.DisplayName, target.DisplayName,
                            $"Round {round + 1}: {player.DisplayName} attacks {target.DisplayName} for {dmg.Total} damage.");
                    }
                }

                foreach (var npc in npcs.Where(n => n.CurrentHP > 0))
                {
                    var target = players.FirstOrDefault(p => p.CurrentHP > 0);
                    if (target != null)
                    {
                        var dmg = _diceEngine.Roll("1d8");
                        target.CurrentHP = Math.Max(0, target.CurrentHP - dmg.Total);
                        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                            CombatEventType.Damage, npc.DisplayName, target.DisplayName,
                            $"Round {round + 1}: {npc.DisplayName} attacks {target.DisplayName} for {dmg.Total} damage.");
                    }
                }
            }

            var playersAlive = players.Count(p => p.CurrentHP > 0);
            var npcsAlive = npcs.Count(n => n.CurrentHP > 0);

            string result;
            if (playersAlive == 0)
                result = "Players were defeated";
            else if (npcsAlive == 0)
                result = "All NPCs were defeated";
            else
                result = $"Combat ended with {playersAlive} player(s) and {npcsAlive} NPC(s) remaining";

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.CombatEnd, "System", "System",
                $"Dramatic resolution: {result}");
        }

        var livingPlayers = combat.Participants.Count(p => p.ParticipantType == "Player" && p.CurrentHP > 0);
        var livingNPCs = combat.Participants.Count(p => p.ParticipantType == "NPC" && p.CurrentHP > 0);

        if (livingPlayers == 0 || livingNPCs == 0)
        {
            await EndCombatAsync(combatId, "Combat resolved by auto-resolution");
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    private async Task EndCombatAsync(Guid combatId, string? result = null)
    {
        var combat = await GetCombatWithParticipants(combatId);
        if (combat == null) return;

        combat.Status = CombatStatus.Finished;
        combat.EndedAt = DateTime.UtcNow;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
    }

    private async Task AddCombatEvent(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        _context.CombatEvents.Add(evt);
        await _context.SaveChangesAsync();
    }
}
