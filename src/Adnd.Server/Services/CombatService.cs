using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Core combat service managing encounters, turns, initiative, and combat events.
/// </summary>
public interface ICombatService
{
    Task<Combat> StartCombatAsync(Guid gameId, Guid? sessionId, string? name = null);
    Task<Combat> EndCombatAsync(Guid combatId, string? result = null);
    Task<Combat> PauseCombatAsync(Guid combatId);
    Task<Combat> ResumeCombatAsync(Guid combatId);

    Task<CombatParticipant> AddParticipantAsync(Guid combatId, string participantType,
        Guid? playerId, Guid? npcId, string displayName, int ac, int currentHP, int maxHP,
        JsonElement? conditions = null, JsonElement? savingThrows = null,
        JsonElement? deathSaveState = null);
    Task<Combat> RemoveParticipantAsync(Guid combatId, Guid participantId);

    Task<(CombatParticipant participant, int[] rolls)> RollInitiativeAsync(Guid combatId, Guid participantId, string formula = "1d20");
    Task<Combat> RollInitiativeForAllAsync(Guid combatId, string formula = "1d20");
    Task<Combat> ReorderInitiativeAsync(Guid combatId, List<Guid> participantIdsInOrder);

    Task<Combat> AdvanceTurnAsync(Guid combatId);
    Task<Combat> RetreatTurnAsync(Guid combatId);
    Task<CombatParticipant> GetCurrentTurnParticipantAsync(Guid combatId);
    Task<Combat> SetCurrentTurnAsync(Guid combatId, Guid participantId);

    Task<AttackWithCombatResult> ExecuteAttackAsync(Guid combatId, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus = null, string? damageFormula = null,
        int? damageBonus = null, string? description = null);

    Task<SaveThrowResult> ExecuteSaveThrowAsync(Guid combatId, string participantName, Guid participantId,
        string saveType, string saveFormula, int dc);

    Task<Combat> ApplyConditionAsync(Guid combatId, Guid participantId, string conditionName,
        int? duration = null, string? description = null);
    Task<Combat> RemoveConditionAsync(Guid combatId, Guid participantId, string conditionName);
    Task<Combat> ClearConditionsAsync(Guid combatId, Guid participantId, string? exceptCondition = null);

    Task<Combat> DealDamageAsync(Guid combatId, Guid participantId, int damage, string? source = null);
    Task<Combat> DealTemporaryHPAsync(Guid combatId, Guid participantId, int tempHP);
    Task<Combat> HealAsync(Guid combatId, Guid participantId, int amount, string? source = null);

    Task<DeathSaveResult> MakeDeathSaveAsync(Guid combatId, Guid participantId, bool success);
    Task<Combat> AddDeathSaveSuccessAsync(Guid combatId, Guid participantId);
    Task<Combat> AddDeathSaveFailureAsync(Guid combatId, Guid participantId);

    // Spell combat
    Task<SpellCastResult> CastSpellAsync(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null);
    Task<SpellCastResult> CastAreaSpellAsync(Guid combatId, string casterName, string spellName,
        string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null, Guid[]? targetIds = null);

    // System-specific rules
    Task<Combat> ApplySystemSpecificEffectsAsync(Guid combatId, string systemId, Guid participantId);
    Task<Combat> CalculateProficiencyBonusAsync(Guid combatId, string systemId, int level);
    Task<Combat> CalculateSavingThrowAsync(Guid combatId, string systemId, string saveType,
        Guid participantId, int? proficiencyBonus = null);

    // Character progression
    Task<Combat> AddXPAsync(Guid combatId, Guid participantId, int xpAmount, string reason);
    Task<Combat> LevelUpAsync(Guid combatId, Guid participantId, int newLevel, string systemId);
    Task<Combat> CalculateXPForCombatAsync(Guid combatId, string systemId);

    // Rest system
    Task<Combat> StartShortRestAsync(Guid combatId);
    Task<Combat> StartLongRestAsync(Guid combatId);
    Task<Combat> EndRestAsync(Guid combatId);
    Task<RestResult> GetCurrentRestStatusAsync(Guid combatId);

    // Inventory/Equipment
    Task<Combat> AddItemToParticipantAsync(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null);
    Task<Combat> RemoveItemFromParticipantAsync(Guid combatId, Guid participantId, string itemName);
    Task<Combat> EquipItemAsync(Guid combatId, Guid participantId, string itemName);
    Task<Combat> UnequipItemAsync(Guid combatId, Guid participantId, string itemName);

    // Grid/Map
    Task<Combat> SetParticipantPositionAsync(Guid combatId, Guid participantId, int gridX, int gridY);
    Task<Combat> MoveParticipantAsync(Guid combatId, Guid participantId, int newGridX, int newGridY);
    Task<Combat> SetGridSizeAsync(Guid combatId, int width, int height);
    Task<GridPosition?> GetParticipantPositionAsync(Guid combatId, Guid participantId);
    Task<List<GridPosition>> GetAdjacentPositionsAsync(Guid combatId, int gridX, int gridY, int range = 1);

    // AI Combat
    Task<AICombatSuggestion> GetAITacticalSuggestionsAsync(Guid combatId);
    Task<AICombatSuggestion> GetAINPCBehaviorAsync(Guid combatId, Guid npcId);
    Task<Combat> AutoResolveCombatAsync(Guid combatId, string resolutionMode = "quick");

    // SAN (CoC specific)
    Task<Combat> ApplySANLossAsync(Guid combatId, Guid participantId, int sanLoss, string reason);
    Task<Combat> ApplySANRecoveryAsync(Guid combatId, Guid participantId, int sanRecovery);
    Task<SanityCheckResult> MakeSANCheckAsync(Guid combatId, Guid participantId, int dc);

    Task<CombatLog> GetCombatLogAsync(Guid combatId);
    Task<Combat?> GetCombatAsync(Guid combatId);
    Task<List<Combat>> GetActiveCombatAsync(Guid gameId);
}

public class CombatService : ICombatService
{
    private readonly AppDbContext _context;
    private readonly IDiceEngine _diceEngine;
    private readonly ILogger<CombatService> _logger;

    public CombatService(AppDbContext context, IDiceEngine diceEngine, ILogger<CombatService> logger)
    {
        _context = context;
        _diceEngine = diceEngine;
        _logger = logger;
    }

    // ==================== Combat Lifecycle ====================

    public async Task<Combat> StartCombatAsync(Guid gameId, Guid? sessionId, string? name = null)
    {
        var combat = new Combat
        {
            GameId = gameId,
            SessionId = sessionId,
            Name = name,
            Status = CombatStatus.Active,
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            Participants = new List<CombatParticipant>(),
            Events = new List<CombatEvent>()
        };

        _context.Combats.Add(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, 0, 0, CombatEventType.CombatStart, "System", "System",
            $"Combat '{name}' started.");

        _logger.LogInformation("Combat started: {CombatId} in game {GameId}", combat.Id, gameId);
        return combat;
    }

    public async Task<Combat> EndCombatAsync(Guid combatId, string? result = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Finished;
        combat.EndedAt = DateTime.UtcNow;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatEnd, "System", "System",
            result ?? "Combat ended.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Combat ended: {CombatId} with result: {Result}", combatId, result ?? "unknown");
        return combat;
    }

    public async Task<Combat> PauseCombatAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Paused;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> ResumeCombatAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Active;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    // ==================== Participants ====================

    public async Task<CombatParticipant> AddParticipantAsync(Guid combatId, string participantType,
        Guid? playerId, Guid? npcId, string displayName, int ac, int currentHP, int maxHP,
        JsonElement? conditions = null, JsonElement? savingThrows = null,
        JsonElement? deathSaveState = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Status != CombatStatus.Active)
            throw new InvalidOperationException("Cannot add participants to a finished combat.");

        var participant = new CombatParticipant
        {
            CombatId = combatId,
            ParticipantType = participantType,
            PlayerId = playerId,
            NpcId = npcId,
            DisplayName = displayName,
            AC = ac,
            CurrentHP = currentHP,
            MaxHP = maxHP,
            Conditions = conditions ?? JsonDocument.Parse("[]").RootElement,
            SavingThrows = savingThrows,
            DeathSaveState = deathSaveState,
            InitiativeCount = combat.InitiativeCount++
        };

        _context.CombatParticipants.Add(participant);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatStart, participant.DisplayName, participant.DisplayName,
            $"Added to combat (HP: {currentHP}/{maxHP}, AC: {ac}).");

        return participant;
    }

    public async Task<Combat> RemoveParticipantAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null)
            throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        _context.CombatParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatEnd, participant.DisplayName, participant.DisplayName,
            "Removed from combat.");

        // Adjust turn index if needed
        if (combat.CurrentTurnIndex >= combat.Participants.Count)
            combat.CurrentTurnIndex = Math.Max(0, combat.Participants.Count - 1);

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    // ==================== Initiative ====================

    public async Task<(CombatParticipant participant, int[] rolls)> RollInitiativeAsync(Guid combatId, Guid participantId, string formula = "1d20")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var result = _diceEngine.Roll(formula);
        participant.Initiative = result.Total;
        participant.InitiativeCount = combat.InitiativeCount++;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Initiative, participant.DisplayName, participant.DisplayName,
            $"Initiative: {formula} = {result.Total} ({string.Join(',', result.Rolls)})");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        return (participant, result.Rolls);
    }

    public async Task<Combat> RollInitiativeForAllAsync(Guid combatId, string formula = "1d20")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        foreach (var participant in combat.Participants)
        {
            var result = _diceEngine.Roll(formula);
            participant.Initiative = result.Total;
            participant.InitiativeCount = combat.InitiativeCount++;

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Initiative, participant.DisplayName, participant.DisplayName,
                $"Initiative: {formula} = {result.Total} ({string.Join(',', result.Rolls)})");
        }

        // Sort by initiative (desc), then by initiativeCount (asc) for tiebreak
        combat.Participants = combat.Participants
            .OrderByDescending(p => p.Initiative)
            .ThenBy(p => p.InitiativeCount)
            .ToList();

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        // Build turn order string
        var turnOrder = string.Join(", ", combat.Participants.Select(p => $"{p.DisplayName} ({p.Initiative})"));
        await AddCombatEvent(combat, combat.CurrentRound, 0, CombatEventType.RoundStart, "System", "System",
            $"Initiative order: {turnOrder}");

        return combat;
    }

    public async Task<Combat> ReorderInitiativeAsync(Guid combatId, List<Guid> participantIdsInOrder)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Participants = participantIdsInOrder
            .Select(id => combat.Participants.First(p => p.Id == id))
            .ToList();

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    // ==================== Turn Management ====================

    public async Task<Combat> AdvanceTurnAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Participants.Count == 0)
            throw new InvalidOperationException("No participants in combat.");

        var oldTurnIndex = combat.CurrentTurnIndex;
        combat.CurrentTurnIndex++;

        if (combat.CurrentTurnIndex >= combat.Participants.Count)
        {
            // New round
            combat.CurrentRound++;
            combat.CurrentTurnIndex = 0;

            // Process end-of-round effects on conditions
            foreach (var participant in combat.Participants)
            {
                var conditions = await GetConditions(participant);
                // Reduce duration on conditions with duration > 0
                var updatedConditions = new List<ConditionEntry>();
                foreach (var cond in conditions)
                {
                    if (cond.Duration > 0)
                    {
                        cond.Duration--;
                        if (cond.Duration > 0)
                            updatedConditions.Add(cond);
                        else
                        {
                            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                                CombatEventType.Condition, "System", participant.DisplayName,
                                $"Condition '{cond.Name}' expired.");
                        }
                    }
                }
                participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(updatedConditions)).RootElement;
            }
        }

        var newTurnParticipant = combat.Participants[combat.CurrentTurnIndex];

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.TurnChange, "System", "System",
            $"Round {combat.CurrentRound}, Turn: {newTurnParticipant.DisplayName}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> RetreatTurnAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.CurrentTurnIndex > 0)
        {
            combat.CurrentTurnIndex--;
        }
        else if (combat.CurrentRound > 1)
        {
            combat.CurrentRound--;
            combat.CurrentTurnIndex = combat.Participants.Count - 1;
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<CombatParticipant> GetCurrentTurnParticipantAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Participants.Count == 0)
            throw new InvalidOperationException("No participants in combat.");

        return combat.Participants[combat.CurrentTurnIndex];
    }

    public async Task<Combat> SetCurrentTurnAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var idx = combat.Participants.FindIndex(p => p.Id == participantId);
        if (idx < 0)
            throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        combat.CurrentTurnIndex = idx;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    // ==================== Combat Actions ====================

    public async Task<AttackWithCombatResult> ExecuteAttackAsync(Guid combatId, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus = null, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var target = combat.Participants.FirstOrDefault(p => p.Id == targetId)
            ?? throw new KeyNotFoundException($"Target participant {targetId} not found in combat.");

        // Attack roll
        var attackResult = _diceEngine.Roll(attackFormula);
        var totalAttack = attackResult.Total + (attackBonus ?? 0);
        var hit = totalAttack >= target.AC;

        // Check for critical (natural 20)
        var isCrit = attackResult.Rolls.Length == 1 && attackResult.Rolls[0] == 20;

        // Check for fumble (natural 1)
        var isFumble = attackResult.Rolls.Length == 1 && attackResult.Rolls[0] == 1;

        string damageInfo = string.Empty;
        int damageTotal = 0;

        if (hit)
        {
            if (isCrit)
            {
                // Critical: roll damage dice twice
                if (damageFormula != null)
                {
                    var critDamage = _diceEngine.Roll(damageFormula);
                    var doubledDice = critDamage.Rolls.Concat(critDamage.Rolls).ToArray();
                    damageTotal = doubledDice.Sum() + (damageBonus ?? 0);
                    damageInfo = $"CRITICAL! {damageFormula} doubled + {damageBonus ?? 0} = {damageTotal}";
                }
            }
            else if (damageFormula != null)
            {
                var dmgResult = _diceEngine.Roll(damageFormula);
                damageTotal = dmgResult.Total + (damageBonus ?? 0);
                damageInfo = $"{damageFormula} + {damageBonus ?? 0} = {damageTotal}";

                // Apply damage
                await DealDamageToParticipant(combat, target, damageTotal, attackerName);
            }
        }
        else if (isFumble)
        {
            damageInfo = "Fumble! Weapon malfunctioned.";
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, attackerName, attackerName, "Fumbled attack.");
        }

        var result = new AttackWithCombatResult
        {
            Attacker = attackerName,
            Weapon = weapon,
            Target = target.DisplayName,
            Hit = hit,
            IsCritical = isCrit,
            IsFumble = isFumble,
            AttackRoll = totalAttack,
            AttackDice = attackResult.Total,
            AC = target.AC,
            DamageDice = damageFormula ?? string.Empty,
            DamageTotal = damageTotal,
            DamageInfo = damageInfo,
            TargetHP = target.CurrentHP,
            TargetMaxHP = target.MaxHP
        };

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Attack, attackerName, target.DisplayName,
            $"{weapon}: {attackFormula}+{attackBonus ?? 0}={totalAttack} vs AC {target.AC} -> {(hit ? "HIT" : "MISS")}"
            + (hit ? $" | {damageInfo}" : ""));

        return result;
    }

    public async Task<SaveThrowResult> ExecuteSaveThrowAsync(Guid combatId, string participantName, Guid participantId,
        string saveType, string saveFormula, int dc)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var rollResult = _diceEngine.Roll(saveFormula);
        var total = rollResult.Total;
        var success = total >= dc;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, participantName, participantName,
            $"{saveType}: {saveFormula}={total} vs DC {dc} -> {(success ? "SUCCESS" : "FAILURE")}");

        return new SaveThrowResult
        {
            Participant = participant.DisplayName,
            SaveType = saveType,
            DiceRoll = total,
            DC = dc,
            Success = success,
            RolledAt = DateTime.UtcNow
        };
    }

    // ==================== Conditions ====================

    public async Task<Combat> ApplyConditionAsync(Guid combatId, Guid participantId, string conditionName,
        int? duration = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);

        // Check if condition already exists - refresh duration if so
        var existingIdx = conditions.FindIndex(c => c.Name.ToLower() == conditionName.ToLower());
        if (existingIdx >= 0)
        {
            if (duration.HasValue && duration.Value > 0)
            {
                conditions[existingIdx].Duration = duration.Value;
                conditions[existingIdx].Description = description;
            }
        }
        else
        {
            conditions.Add(new ConditionEntry
            {
                Name = conditionName,
                Duration = duration ?? 0,
                Description = description
            });
        }

        participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Applied '{conditionName}'{(duration.HasValue && duration.Value > 0 ? $" for {duration.Value} round(s)" : "")}.");

        return combat;
    }

    public async Task<Combat> RemoveConditionAsync(Guid combatId, Guid participantId, string conditionName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);
        var removed = conditions.RemoveAll(c => c.Name.ToLower() == conditionName.ToLower());

        if (removed > 0)
        {
            participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
            _context.Combats.Update(combat);
            await _context.SaveChangesAsync();

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Removed '{conditionName}'.");
        }

        return combat;
    }

    public async Task<Combat> ClearConditionsAsync(Guid combatId, Guid participantId, string? exceptCondition = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);
        var cleared = exceptCondition == null
            ? conditions.Count
            : conditions.RemoveAll(c => c.Name.ToLower() != exceptCondition.ToLower());

        if (cleared > 0)
        {
            participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
            _context.Combats.Update(combat);
            await _context.SaveChangesAsync();

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Cleared {(cleared == conditions.Count + cleared ? "all conditions" : $"{cleared} conditions")}.");
        }

        return combat;
    }

    // ==================== HP Management ====================

    public async Task<Combat> DealDamageAsync(Guid combatId, Guid participantId, int damage, string? source = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        return await DealDamageToParticipant(combat, participant, damage, source ?? "unknown");
    }

    public async Task<Combat> DealTemporaryHPAsync(Guid combatId, Guid participantId, int tempHP)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var currentTemp = participant.TemporaryHP?.GetInt32() ?? 0;
        participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(currentTemp + tempHP)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, "System", participant.DisplayName,
            $"Gained {tempHP} temporary HP (total: {currentTemp + tempHP}).");

        return combat;
    }

    public async Task<Combat> HealAsync(Guid combatId, Guid participantId, int amount, string? source = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var oldHP = participant.CurrentHP;
        participant.CurrentHP = Math.Min(participant.MaxHP, participant.CurrentHP + amount);
        var actualHeal = participant.CurrentHP - oldHP;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, source ?? "System", participant.DisplayName,
            $"Healed {actualHeal} HP ({oldHP} -> {participant.CurrentHP}/{participant.MaxHP}).");

        return combat;
    }

    // ==================== Death Saves ====================

    public async Task<DeathSaveResult> MakeDeathSaveAsync(Guid combatId, Guid participantId, bool success)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);

        if (success)
        {
            deathState.Successes++;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.DeathSave, participant.DisplayName, participant.DisplayName,
                $"Death save SUCCESS ({deathState.Successes}/3 successes).");

            if (deathState.Successes >= 3)
            {
                participant.CurrentHP = 1;
                participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Revival, "System", participant.DisplayName,
                    "Stabilized! Revived to 1 HP.");
            }
        }
        else
        {
            deathState.Failures++;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.DeathSave, participant.DisplayName, participant.DisplayName,
                $"Death save FAILURE ({deathState.Failures}/3 failures).");

            if (deathState.Failures >= 3)
            {
                participant.CurrentHP = 0;
                participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Death, participant.DisplayName, participant.DisplayName,
                    "DIED from death saves!");
            }
        }

        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        return new DeathSaveResult
        {
            Participant = participant.DisplayName,
            Success = success,
            Successes = deathState.Successes,
            Failures = deathState.Failures,
            IsStabilized = deathState.Successes >= 3,
            IsDead = deathState.Failures >= 3,
            RolledAt = DateTime.UtcNow
        };
    }

    public async Task<Combat> AddDeathSaveSuccessAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);
        deathState.Successes++;
        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        if (deathState.Successes >= 3)
        {
            participant.CurrentHP = 1;
            participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Revival, "System", participant.DisplayName,
                "Stabilized! Revived to 1 HP.");
        }

        return combat;
    }

    public async Task<Combat> AddDeathSaveFailureAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);
        deathState.Failures++;
        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        if (deathState.Failures >= 3)
        {
            participant.CurrentHP = 0;
            participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Death, participant.DisplayName, participant.DisplayName,
                "DIED from death saves!");
        }

        return combat;
    }

    // ==================== Query Methods ====================

    public async Task<CombatLog> GetCombatLogAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var events = await _context.CombatEvents
            .Where(e => e.CombatId == combatId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        return new CombatLog
        {
            CombatId = combatId,
            Name = combat.Name,
            Status = combat.Status,
            CurrentRound = combat.CurrentRound,
            CurrentTurnIndex = combat.CurrentTurnIndex,
            Participants = combat.Participants
                .OrderBy(p => p.Initiative)
                .ThenByDescending(p => p.InitiativeCount)
                .Select(p => new ParticipantSummary
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    ParticipantType = p.ParticipantType,
                    CurrentHP = p.CurrentHP,
                    MaxHP = p.MaxHP,
                    AC = p.AC,
                    Initiative = p.Initiative,
                    Conditions = JsonSerializer.Deserialize<List<ConditionEntry>>(p.Conditions.ToString()) ?? new(),
                    IsCurrentTurn = p.Id == combat.Participants[Math.Min(combat.CurrentTurnIndex, combat.Participants.Count - 1)].Id,
                    IsDead = p.CurrentHP <= 0
                })
                .ToList(),
            Events = events.Select(e => new CombatLogEvent
            {
                Id = e.Id,
                Round = e.Round,
                TurnIndex = e.TurnIndex,
                Type = e.Type,
                ActorName = e.ActorName,
                TargetName = e.TargetName,
                Content = e.Content,
                Metadata = e.Metadata,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    public async Task<Combat?> GetCombatAsync(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    public async Task<List<Combat>> GetActiveCombatAsync(Guid gameId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Where(c => c.GameId == gameId && c.Status == CombatStatus.Active)
            .OrderByDescending(c => c.StartedAt)
            .ToListAsync();
    }

    // ==================== Helpers ====================

    private async Task<Combat> DealDamageToParticipant(Combat combat, CombatParticipant participant, int damage, string source)
    {
        // Apply damage: first to temporary HP, then to current HP
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
                return combat;
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

        // Check for death - start death saves if HP is 0 or below
        if (participant.CurrentHP <= 0)
        {
            // Start death saves if not already in them
            var ds = GetDeathSaveState(participant);
            if (ds.Successes == 0 && ds.Failures == 0)
            {
                participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(new DeathSaveState { Successes = 0, Failures = 0 })).RootElement;
            }
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<List<ConditionEntry>> GetConditions(CombatParticipant participant)
    {
        if (participant.Conditions.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<ConditionEntry>>(participant.Conditions.ToString())
                ?? new List<ConditionEntry>();
        }
        return new List<ConditionEntry>();
    }

    private DeathSaveState GetDeathSaveState(CombatParticipant participant)
    {
        if (participant.DeathSaveState.HasValue &&
            participant.DeathSaveState.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
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

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    // ==================== Spell Combat ====================

    public async Task<SpellCastResult> CastSpellAsync(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var target = combat.Participants.FirstOrDefault(p => p.Id == targetId)
            ?? throw new KeyNotFoundException($"Target participant {targetId} not found in combat.");

        // Target makes saving throw
        var saveResult = _diceEngine.Roll(saveFormula);
        var saveTotal = saveResult.Total;
        var saveSuccess = saveTotal >= saveDC;

        // Check for critical save (natural 20)
        var isCritSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 20;
        if (isCritSave) saveSuccess = true; // Auto-success on natural 20

        // Check for fumble save (natural 1)
        var isFumbleSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 1;
        if (isFumbleSave) saveSuccess = false; // Auto-failure on natural 1

        int damageTotal = 0;
        string damageInfo = string.Empty;
        string effect = string.Empty;

        // Apply spell damage or effect
        if (damageFormula != null && !saveSuccess)
        {
            var dmgResult = _diceEngine.Roll(damageFormula);
            damageTotal = dmgResult.Total + (damageBonus ?? 0);
            damageInfo = $"{damageFormula} + {damageBonus ?? 0} = {damageTotal}";
            await DealDamageToParticipant(combat, target, damageTotal, casterName);
        }
        else if (damageFormula != null && saveSuccess)
        {
            // Half damage on successful save (D&D 5e style)
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

    // ==================== System-Specific Rules ====================

    public async Task<Combat> ApplySystemSpecificEffectsAsync(Guid combatId, string systemId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);

        switch (systemId)
        {
            case "dnd5e":
                // Apply exhaustion levels if applicable
                var exhaustion = conditions.FirstOrDefault(c => c.Name.ToLower() == "exhaustion");
                if (exhaustion != null && exhaustion.Duration > 0)
                {
                    // Reduce exhaustion duration
                    exhaustion.Duration--;
                    if (exhaustion.Duration <= 0)
                    {
                        conditions.Remove(exhaustion);
                        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                            CombatEventType.Condition, "System", participant.DisplayName,
                            "Exhaustion level removed.");
                    }
                }
                break;

            case "coc7e":
                // CoC: Check for SAN loss from combat
                // This would be triggered by specific events
                break;

            case "pf2e":
                // PF2e: Handle conditions specific to Pathfinder
                break;
        }

        participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> CalculateProficiencyBonusAsync(Guid combatId, string systemId, int level)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        int proficiencyBonus = 0;

        if (systemId == "dnd5e")
        {
            // D&D 5e proficiency progression
            if (level <= 4) proficiencyBonus = 2;
            else if (level <= 8) proficiencyBonus = 3;
            else if (level <= 12) proficiencyBonus = 4;
            else if (level <= 16) proficiencyBonus = 5;
            else proficiencyBonus = 6;
        }
        else if (systemId == "pf2e")
        {
            // PF2e proficiency progression
            proficiencyBonus = 2 + (level - 1) / 2;
        }
        // CoC doesn't use proficiency bonus

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            $"Proficiency bonus for level {level}: {proficiencyBonus}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> CalculateSavingThrowAsync(Guid combatId, string systemId, string saveType,
        Guid participantId, int? proficiencyBonus = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var savingThrows = GetSavingThrows(participant);
        var baseSave = savingThrows.ContainsKey(saveType.ToLower()) ? savingThrows[saveType.ToLower()] : 0;

        string result;
        if (systemId == "dnd5e")
        {
            var prof = proficiencyBonus ?? 2;
            result = $"{saveType}: {baseSave} + {prof} = {baseSave + prof}";
        }
        else if (systemId == "pf2e")
        {
            var prof = proficiencyBonus ?? 2;
            result = $"{saveType}: {baseSave} + {prof} = {baseSave + prof}";
        }
        else
        {
            result = $"{saveType}: {baseSave}";
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, "System", participant.DisplayName,
            result);

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    // ==================== Character Progression ====================

    public async Task<Combat> AddXPAsync(Guid combatId, Guid participantId, int xpAmount, string reason)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Track XP in participant notes (JSON)
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

        var oldLevel = participant.MaxHP; // Using MaxHP field to track level for now
        participant.MaxHP = newLevel;

        // Calculate new HP based on system
        int newMaxHP;
        if (systemId == "dnd5e")
        {
            // Simplified: +2 HP per level
            newMaxHP = participant.CurrentHP + 2;
        }
        else if (systemId == "pf2e")
        {
            // PF2e: +6 HP + CON mod per level
            newMaxHP = participant.CurrentHP + 6;
        }
        else
        {
            newMaxHP = participant.CurrentHP + 2;
        }

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

        // Calculate XP based on defeated NPCs
        var defeatedNPCs = combat.Participants.Where(p =>
            p.ParticipantType == "NPC" && p.CurrentHP <= 0).ToList();

        int totalXP = 0;
        foreach (var npc in defeatedNPCs)
        {
            // Simplified XP calculation
            var xp = 100; // Base XP per NPC
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

    // ==================== Rest System ====================

    public async Task<Combat> StartShortRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Name = combat.Name + " (Short Rest)";

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Short rest started. Participants can recover HP and resources.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> StartLongRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Name = combat.Name + " (Long Rest)";

        // Recover all HP
        foreach (var participant in combat.Participants)
        {
            if (participant.CurrentHP < participant.MaxHP)
            {
                var oldHP = participant.CurrentHP;
                participant.CurrentHP = participant.MaxHP;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Healing, "System", participant.DisplayName,
                    $"Long rest recovery: {oldHP} -> {participant.CurrentHP} HP");
            }
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Long rest completed. All HP recovered.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> EndRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Rest ended. Combat may resume.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<RestResult> GetCurrentRestStatusAsync(Guid combatId)
    {
        var combat = await GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        return new RestResult
        {
            RestType = combat.Name?.Contains("Short") == true ? "Short" :
                       combat.Name?.Contains("Long") == true ? "Long" : "None",
            IsInProgress = combat.Name?.Contains("Rest") == true,
            HPRecovered = combat.Participants.Sum(p => Math.Max(0, p.MaxHP - p.CurrentHP)),
            Effects = new List<string> { "HP recovery", "Resource recovery" }
        };
    }

    // ==================== Inventory/Equipment ====================

    public async Task<Combat> AddItemToParticipantAsync(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Track inventory in participant notes
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

    // ==================== Grid/Map ====================

    public async Task<Combat> SetGridSizeAsync(Guid combatId, int width, int height)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        // Store grid size in combat notes
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

        // Store position in participant notes
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

        // Check movement cost
        var notes = GetNotes(participant);
        var moveSpeed = 30; // Default 30 feet
        if (notes != null && notes.ContainsKey("moveSpeed"))
        {
            moveSpeed = Convert.ToInt32(notes["moveSpeed"]);
        }

        // Calculate distance (Manhattan distance for grid)
        var currentX = notes != null && notes.ContainsKey("gridX") ? Convert.ToInt32(notes["gridX"]) : 0;
        var currentY = notes != null && notes.ContainsKey("gridY") ? Convert.ToInt32(notes["gridY"]) : 0;
        var distance = Math.Abs(newGridX - currentX) + Math.Abs(newGridY - currentY);

        // Each 5 feet = 1 grid square
        var squaresMoved = (distance + 4) / 5; // Ceiling division
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

    public async Task<List<GridPosition>> GetAdjacentPositionsAsync(Guid combatId, int gridX, int gridY, int range = 1)
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

        return positions;
    }

    // ==================== AI Combat ====================

    public async Task<AICombatSuggestion> GetAITacticalSuggestionsAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var suggestions = new AICombatSuggestion();

        // Analyze combat state
        var livingParticipants = combat.Participants.Where(p => p.CurrentHP > 0).ToList();
        var deadParticipants = combat.Participants.Where(p => p.CurrentHP <= 0).ToList();
        var lowHPParticipants = livingParticipants.Where(p => p.CurrentHP < p.MaxHP * 0.3).ToList();
        var highThreatNPCs = combat.Participants.Where(p =>
            p.ParticipantType == "NPC" && p.CurrentHP > p.MaxHP * 0.5).ToList();

        // Determine threat level
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

        // Generate tactical suggestions for living players
        foreach (var player in livingParticipants.Where(p => p.ParticipantType == "Player"))
        {
            // Suggest attacking lowest HP enemy
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

            // Suggest healing if low HP
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

            // Suggest using abilities/spells
            suggestions.Suggestions.Add(new AITacticalAction
            {
                Actor = player.DisplayName,
                Action = "Use Ability/Spell",
                Target = "",
                Reason = "Consider using class abilities or spells",
                Priority = 3
            });
        }

        // Generate NPC behavior suggestions
        foreach (var npc in highThreatNPCs)
        {
            // Find closest living player
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

        // Generate warnings
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

        // Recommended strategy
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

        // Determine NPC behavior based on HP and situation
        string behavior;
        string targetAction;
        string reason;

        if (npc.CurrentHP <= 0)
        {
            behavior = "Dead";
            targetAction = "None";
            reason = "NPC is downed";
        }
        else if (npc.CurrentHP < npc.MaxHP * 0.3)
        {
            behavior = "Fleeing";
            targetAction = "Retreat";
            reason = "Low HP - should flee from combat";
        }
        else if (npc.CurrentHP < npc.MaxHP * 0.6)
        {
            behavior = "Cautious";
            targetAction = "Defensive";
            reason = "Moderate damage - use caution";
        }
        else
        {
            behavior = "Aggressive";
            targetAction = "Attack";
            reason = "Full HP - aggressive stance";
        }

        // Find target
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
            // Quick resolution: compare total HP and initiative
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
            // Dramatic resolution: simulate a few rounds
            for (int round = 0; round < 3; round++)
            {
                // Players attack
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

                // NPCs attack
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

        // Check if combat should end
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

    // ==================== SAN (CoC Specific) ====================

    public async Task<Combat> ApplySANLossAsync(Guid combatId, Guid participantId, int sanLoss, string reason)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Track SAN in participant notes
        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);
        var newSAN = Math.Max(0, currentSAN - sanLoss);
        notes["currentSAN"] = newSAN;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"SAN loss: {currentSAN} -> {newSAN} ({sanLoss} lost) - {reason}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> ApplySANRecoveryAsync(Guid combatId, Guid participantId, int sanRecovery)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);
        var newSAN = Math.Min(maxSAN, currentSAN + sanRecovery);
        notes["currentSAN"] = newSAN;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, "System", participant.DisplayName,
            $"SAN recovery: {currentSAN} -> {newSAN} (+{sanRecovery})");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<SanityCheckResult> MakeSANCheckAsync(Guid combatId, Guid participantId, int dc)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);

        var rollResult = _diceEngine.Roll("d100");
        var roll = rollResult.Total;
        var success = roll <= currentSAN;
        var isCritical = roll <= (currentSAN / 5);
        var isFumble = roll >= 95;

        int sanLoss = 0;
        string effect = string.Empty;

        if (success)
        {
            if (isCritical)
            {
                sanLoss = 0;
                effect = "Critical success - no SAN loss";
            }
            else if (isFumble)
            {
                var fumbleDmg = _diceEngine.Roll("1d10");
                sanLoss = fumbleDmg.Total;
                effect = $"Fumble! Lost {sanLoss} SAN";
            }
            else
            {
                sanLoss = 0;
                effect = "Success - no SAN loss";
            }
        }
        else
        {
            var sanDmg = _diceEngine.Roll("1d10");
            sanLoss = sanDmg.Total;
            effect = $"Failed - lost {sanLoss} SAN";
        }

        var newSAN = Math.Max(0, currentSAN - sanLoss);
        notes["currentSAN"] = newSAN;
        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, participant.DisplayName, participant.DisplayName,
            $"SAN check: d100={roll} vs {currentSAN} -> {(success ? "SUCCESS" : "FAILURE")} | SAN: {currentSAN} -> {newSAN} (-{sanLoss})");

        return new SanityCheckResult
        {
            Participant = participant.DisplayName,
            CurrentSAN = currentSAN,
            Roll = roll,
            DC = dc,
            Success = success,
            IsCritical = isCritical,
            SANLoss = sanLoss,
            Effect = effect,
            RolledAt = DateTime.UtcNow
        };
    }

    // ==================== Helpers ====================

    private Dictionary<string, object>? GetNotes(CombatParticipant participant)
    {
        if (participant.Notes.HasValue &&
            participant.Notes.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(participant.Notes.ToString());
        }
        return null;
    }

    private Dictionary<string, int> GetSavingThrows(CombatParticipant participant)
    {
        if (participant.SavingThrows.HasValue &&
            participant.SavingThrows.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(participant.SavingThrows.ToString())
                ?? new Dictionary<string, int>();
        }
        return new Dictionary<string, int>();
    }
}

// ============= DTOs =============

public class AttackWithCombatResult
{
    public string Attacker { get; set; } = string.Empty;
    public string Weapon { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Hit { get; set; }
    public bool IsCritical { get; set; }
    public bool IsFumble { get; set; }
    public int AttackRoll { get; set; }
    public int AttackDice { get; set; }
    public int AC { get; set; }
    public string DamageDice { get; set; } = string.Empty;
    public int DamageTotal { get; set; }
    public string DamageInfo { get; set; } = string.Empty;
    public int TargetHP { get; set; }
    public int TargetMaxHP { get; set; }
}

public class SaveThrowResult
{
    public string Participant { get; set; } = string.Empty;
    public string SaveType { get; set; } = string.Empty;
    public int DiceRoll { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
    public DateTime RolledAt { get; set; }
}

public class DeathSaveResult
{
    public string Participant { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public bool IsStabilized { get; set; }
    public bool IsDead { get; set; }
    public DateTime RolledAt { get; set; }
}

public class CombatLog
{
    public Guid CombatId { get; set; }
    public string? Name { get; set; }
    public CombatStatus Status { get; set; }
    public int CurrentRound { get; set; }
    public int CurrentTurnIndex { get; set; }
    public List<ParticipantSummary> Participants { get; set; } = new();
    public List<CombatLogEvent> Events { get; set; } = new();
}

public class ParticipantSummary
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int AC { get; set; }
    public int Initiative { get; set; }
    public List<ConditionEntry> Conditions { get; set; } = new();
    public bool IsCurrentTurn { get; set; }
    public bool IsDead { get; set; }
}

public class ConditionEntry
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string? Description { get; set; }
}

public class DeathSaveState
{
    public int Successes { get; set; }
    public int Failures { get; set; }
}

public class CombatLogEvent
{
    public Guid Id { get; set; }
    public int Round { get; set; }
    public int TurnIndex { get; set; }
    public CombatEventType Type { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public JsonElement? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}
