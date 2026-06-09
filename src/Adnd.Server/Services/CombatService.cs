using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Core combat service managing encounters, turns, initiative, and combat events.
/// Delegates to domain services for specific concerns.
/// </summary>
public interface ICombatService
{
    // Combat lifecycle
    Task<Combat> StartCombatAsync(Guid gameId, Guid? sessionId, string? name = null);
    Task<Combat> EndCombatAsync(Guid combatId, string? result = null);
    Task<Combat> PauseCombatAsync(Guid combatId);
    Task<Combat> ResumeCombatAsync(Guid combatId);

    // Participants
    Task<CombatParticipant> AddParticipantAsync(Guid combatId, string participantType,
        Guid? playerId, Guid? npcId, string displayName, int ac, int currentHP, int maxHP,
        JsonElement? conditions = null, JsonElement? savingThrows = null,
        JsonElement? deathSaveState = null);
    Task<Combat> RemoveParticipantAsync(Guid combatId, Guid participantId);

    // Initiative
    Task<(CombatParticipant participant, int[] rolls)> RollInitiativeAsync(Guid combatId, Guid participantId, string formula = "1d20");
    Task<Combat> RollInitiativeForAllAsync(Guid combatId, string formula = "1d20");
    Task<Combat> ReorderInitiativeAsync(Guid combatId, List<Guid> participantIdsInOrder);

    // Turns
    Task<Combat> AdvanceTurnAsync(Guid combatId);
    Task<Combat> RetreatTurnAsync(Guid combatId);
    Task<CombatParticipant> GetCurrentTurnParticipantAsync(Guid combatId);
    Task<Combat> SetCurrentTurnAsync(Guid combatId, Guid participantId);

    // Actions (delegates to ICombatActionFactory)
    Task<AttackWithCombatResult> ExecuteAttackAsync(Guid combatId, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus = null, string? damageFormula = null,
        int? damageBonus = null, string? description = null);
    Task<SaveThrowResult> ExecuteSaveThrowAsync(Guid combatId, string participantName, Guid participantId,
        string saveType, string saveFormula, int dc);

    // State (HP, conditions, death saves, rest)
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
    Task<Combat> StartShortRestAsync(Guid combatId);
    Task<Combat> StartLongRestAsync(Guid combatId);
    Task<Combat> EndRestAsync(Guid combatId);
    Task<RestResult> GetCurrentRestStatusAsync(Guid combatId);

    // Spells
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

    // Progression
    Task<Combat> AddXPAsync(Guid combatId, Guid participantId, int xpAmount, string reason);
    Task<Combat> LevelUpAsync(Guid combatId, Guid participantId, int newLevel, string systemId);
    Task<Combat> CalculateXPForCombatAsync(Guid combatId, string systemId);

    // Inventory
    Task<Combat> AddItemToParticipantAsync(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null);
    Task<Combat> RemoveItemFromParticipantAsync(Guid combatId, Guid participantId, string itemName);
    Task<Combat> EquipItemAsync(Guid combatId, Guid participantId, string itemName);
    Task<Combat> UnequipItemAsync(Guid combatId, Guid participantId, string itemName);

    // Grid
    Task<Combat> SetGridSizeAsync(Guid combatId, int width, int height);
    Task<Combat> SetParticipantPositionAsync(Guid combatId, Guid participantId, int gridX, int gridY);
    Task<Combat> MoveParticipantAsync(Guid combatId, Guid participantId, int newGridX, int newGridY);
    Task<GridPosition?> GetParticipantPositionAsync(Guid combatId, Guid participantId);
    Task<List<GridPosition>> GetAdjacentPositionsAsync(Guid combatId, int gridX, int gridY, int range = 1);

    // AI
    Task<AICombatSuggestion> GetAITacticalSuggestionsAsync(Guid combatId);
    Task<AICombatSuggestion> GetAINPCBehaviorAsync(Guid combatId, Guid npcId);
    Task<Combat> AutoResolveCombatAsync(Guid combatId, string resolutionMode = "quick");

    // SAN
    Task<Combat> ApplySANLossAsync(Guid combatId, Guid participantId, int sanLoss, string reason);
    Task<Combat> ApplySANRecoveryAsync(Guid combatId, Guid participantId, int sanRecovery);
    Task<SanityCheckResult> MakeSANCheckAsync(Guid combatId, Guid participantId, int dc);

    // Action economy
    Task<Combat> SpendActionAsync(Guid combatId, Guid participantId);
    Task<Combat> SpendBonusActionAsync(Guid combatId, Guid participantId);
    Task<Combat> SpendReactionAsync(Guid combatId, Guid participantId);
    Task<Combat> SpendMovementAsync(Guid combatId, Guid participantId);
    Task<Combat> RefreshActionsAsync(Guid combatId, Guid participantId);
    Task<Combat> SetActionsAsync(Guid combatId, Guid participantId, int actions, int bonusActions, int reactions, int movements);
    Task<CombatParticipant> GetActionEconomyAsync(Guid combatId, Guid participantId);

    // Queries
    Task<CombatLog> GetCombatLogAsync(Guid combatId);
    Task<Combat?> GetCombatAsync(Guid combatId);
    Task<List<Combat>> GetActiveCombatAsync(Guid gameId);
}

/// <summary>
/// Facade for combat operations, delegating to domain services.
/// This replaces the 20 partial files with a single focused class.
/// </summary>
public class CombatService : ICombatService
{
    private readonly AppDbContext _context;
    private readonly ICombatLifecycleService _lifecycle;
    private readonly ICombatParticipantService _participant;
    private readonly ICombatInitiativeService _initiative;
    private readonly ICombatTurnService _turn;
    private readonly ICombatStateService _state;
    private readonly ICombatSpellService _spell;
    private readonly ICombatInventoryService _inventory;
    private readonly ICombatProgressionService _progression;
    private readonly ICombatGridService _grid;
    private readonly ICombatAIService _ai;
    private readonly ICombatQueryService _query;
    private readonly ISANService _san;
    private readonly ICombatActionFactory _actionFactory;
    private readonly ISystemRulesFactory _rulesFactory;
    private readonly IDiceEngine _diceEngine;
    private readonly ILogger<CombatService> _logger;

    public CombatService(
        AppDbContext context,
        ICombatLifecycleService lifecycle,
        ICombatParticipantService participant,
        ICombatInitiativeService initiative,
        ICombatTurnService turn,
        ICombatStateService state,
        ICombatSpellService spell,
        ICombatInventoryService inventory,
        ICombatProgressionService progression,
        ICombatGridService grid,
        ICombatAIService ai,
        ICombatQueryService query,
        ISANService san,
        ICombatActionFactory actionFactory,
        ISystemRulesFactory rulesFactory,
        IDiceEngine diceEngine,
        ILogger<CombatService> logger)
    {
        _context = context;
        _lifecycle = lifecycle;
        _participant = participant;
        _initiative = initiative;
        _turn = turn;
        _state = state;
        _spell = spell;
        _inventory = inventory;
        _progression = progression;
        _grid = grid;
        _ai = ai;
        _query = query;
        _san = san;
        _actionFactory = actionFactory;
        _rulesFactory = rulesFactory;
        _diceEngine = diceEngine;
        _logger = logger;
    }

    // ===== Combat Lifecycle =====
    public Task<Combat> StartCombatAsync(Guid gameId, Guid? sessionId, string? name = null) => _lifecycle.StartCombatAsync(gameId, sessionId, name);
    public Task<Combat> EndCombatAsync(Guid combatId, string? result = null) => _lifecycle.EndCombatAsync(combatId, result);
    public Task<Combat> PauseCombatAsync(Guid combatId) => _lifecycle.PauseCombatAsync(combatId);
    public Task<Combat> ResumeCombatAsync(Guid combatId) => _lifecycle.ResumeCombatAsync(combatId);

    // ===== Participants =====
    public Task<CombatParticipant> AddParticipantAsync(Guid combatId, string participantType, Guid? playerId, Guid? npcId, string displayName, int ac, int currentHP, int maxHP, JsonElement? conditions = null, JsonElement? savingThrows = null, JsonElement? deathSaveState = null)
        => _participant.AddParticipantAsync(combatId, participantType, playerId, npcId, displayName, ac, currentHP, maxHP, conditions, savingThrows, deathSaveState);
    public Task<Combat> RemoveParticipantAsync(Guid combatId, Guid participantId) => _participant.RemoveParticipantAsync(combatId, participantId);

    // ===== Initiative =====
    public Task<(CombatParticipant participant, int[] rolls)> RollInitiativeAsync(Guid combatId, Guid participantId, string formula = "1d20")
        => _initiative.RollInitiativeAsync(combatId, participantId, formula);
    public Task<Combat> RollInitiativeForAllAsync(Guid combatId, string formula = "1d20") => _initiative.RollInitiativeForAllAsync(combatId, formula);
    public Task<Combat> ReorderInitiativeAsync(Guid combatId, List<Guid> participantIdsInOrder) => _initiative.ReorderInitiativeAsync(combatId, participantIdsInOrder);

    // ===== Turns =====
    public Task<Combat> AdvanceTurnAsync(Guid combatId) => _turn.AdvanceTurnAsync(combatId);
    public Task<Combat> RetreatTurnAsync(Guid combatId) => _turn.RetreatTurnAsync(combatId);
    public Task<CombatParticipant> GetCurrentTurnParticipantAsync(Guid combatId) => _turn.GetCurrentTurnParticipantAsync(combatId);
    public Task<Combat> SetCurrentTurnAsync(Guid combatId, Guid participantId) => _turn.SetCurrentTurnAsync(combatId, participantId);

    // ===== Actions (delegates to ICombatActionFactory) =====
    public async Task<AttackWithCombatResult> ExecuteAttackAsync(Guid combatId, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus = null, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var rules = _rulesFactory.GetRules(combat.GameId.ToString());
        return await AttackAction.ExecuteAsync(combat, attackerName, weapon, targetId, attackFormula,
            attackBonus, damageFormula, damageBonus, description, _context, _diceEngine, rules, _logger);
    }

    public async Task<SaveThrowResult> ExecuteSaveThrowAsync(Guid combatId, string participantName, Guid participantId,
        string saveType, string saveFormula, int dc)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var rules = _rulesFactory.GetRules(combat.GameId.ToString());
        return await SaveThrowAction.ExecuteAsync(combat, participantName, participantId, saveType, saveFormula, dc, _context, _diceEngine, rules, _logger);
    }

    // ===== State =====
    public Task<Combat> ApplyConditionAsync(Guid combatId, Guid participantId, string conditionName, int? duration = null, string? description = null)
        => _state.ApplyConditionAsync(combatId, participantId, conditionName, duration, description);
    public Task<Combat> RemoveConditionAsync(Guid combatId, Guid participantId, string conditionName)
        => _state.RemoveConditionAsync(combatId, participantId, conditionName);
    public Task<Combat> ClearConditionsAsync(Guid combatId, Guid participantId, string? exceptCondition = null)
        => _state.ClearConditionsAsync(combatId, participantId, exceptCondition);
    public Task<Combat> DealDamageAsync(Guid combatId, Guid participantId, int damage, string? source = null)
        => _state.DealDamageAsync(combatId, participantId, damage, source);
    public Task<Combat> DealTemporaryHPAsync(Guid combatId, Guid participantId, int tempHP)
        => _state.DealTemporaryHPAsync(combatId, participantId, tempHP);
    public Task<Combat> HealAsync(Guid combatId, Guid participantId, int amount, string? source = null)
        => _state.HealAsync(combatId, participantId, amount, source);
    public Task<DeathSaveResult> MakeDeathSaveAsync(Guid combatId, Guid participantId, bool success)
        => _state.MakeDeathSaveAsync(combatId, participantId, success);
    public Task<Combat> AddDeathSaveSuccessAsync(Guid combatId, Guid participantId)
        => _state.AddDeathSaveSuccessAsync(combatId, participantId);
    public Task<Combat> AddDeathSaveFailureAsync(Guid combatId, Guid participantId)
        => _state.AddDeathSaveFailureAsync(combatId, participantId);
    public Task<Combat> StartShortRestAsync(Guid combatId) => _state.StartShortRestAsync(combatId);
    public Task<Combat> StartLongRestAsync(Guid combatId) => _state.StartLongRestAsync(combatId);
    public Task<Combat> EndRestAsync(Guid combatId) => _state.EndRestAsync(combatId);
    public Task<RestResult> GetCurrentRestStatusAsync(Guid combatId) => _state.GetCurrentRestStatusAsync(combatId);

    // ===== Spells =====
    public Task<SpellCastResult> CastSpellAsync(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
        => _spell.CastSpellAsync(combatId, casterName, spellName, targetId, saveFormula, saveDC, damageFormula, damageBonus, description);
    public Task<SpellCastResult> CastAreaSpellAsync(Guid combatId, string casterName, string spellName,
        string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null, Guid[]? targetIds = null)
        => _spell.CastAreaSpellAsync(combatId, casterName, spellName, saveFormula, saveDC, damageFormula, damageBonus, description, targetIds);

    // ===== System-Specific Rules =====
    public async Task<Combat> ApplySystemSpecificEffectsAsync(Guid combatId, string systemId, Guid participantId)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var rules = _rulesFactory.GetRules(systemId);
        return await rules.ApplySystemSpecificEffectsAsync(combat, systemId, participantId, _context);
    }

    public async Task<Combat> CalculateProficiencyBonusAsync(Guid combatId, string systemId, int level)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var rules = _rulesFactory.GetRules(systemId);
        var proficiencyBonus = rules.GetProficiencyBonus(level);

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            $"Proficiency bonus for level {level}: {proficiencyBonus}");

        return combat;
    }

    public async Task<Combat> CalculateSavingThrowAsync(Guid combatId, string systemId, string saveType,
        Guid participantId, int? proficiencyBonus = null)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var savingThrows = GetSavingThrows(participant);
        var baseSave = savingThrows.ContainsKey(saveType.ToLower()) ? savingThrows[saveType.ToLower()] : 0;
        var rules = _rulesFactory.GetRules(systemId);
        var prof = proficiencyBonus ?? rules.GetProficiencyBonus(1);

        var result = $"{saveType}: {baseSave} + {prof} = {baseSave + prof}";

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, "System", participant.DisplayName, result);

        return combat;
    }

    // ===== Progression =====
    public Task<Combat> AddXPAsync(Guid combatId, Guid participantId, int xpAmount, string reason)
        => _progression.AddXPAsync(combatId, participantId, xpAmount, reason);
    public Task<Combat> LevelUpAsync(Guid combatId, Guid participantId, int newLevel, string systemId)
        => _progression.LevelUpAsync(combatId, participantId, newLevel, systemId);
    public Task<Combat> CalculateXPForCombatAsync(Guid combatId, string systemId)
        => _progression.CalculateXPForCombatAsync(combatId, systemId);

    // ===== Inventory =====
    public Task<Combat> AddItemToParticipantAsync(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null)
        => _inventory.AddItemToParticipantAsync(combatId, participantId, itemName, itemType, quantity, itemStats);
    public Task<Combat> RemoveItemFromParticipantAsync(Guid combatId, Guid participantId, string itemName)
        => _inventory.RemoveItemFromParticipantAsync(combatId, participantId, itemName);
    public Task<Combat> EquipItemAsync(Guid combatId, Guid participantId, string itemName)
        => _inventory.EquipItemAsync(combatId, participantId, itemName);
    public Task<Combat> UnequipItemAsync(Guid combatId, Guid participantId, string itemName)
        => _inventory.UnequipItemAsync(combatId, participantId, itemName);

    // ===== Grid =====
    public Task<Combat> SetGridSizeAsync(Guid combatId, int width, int height)
        => _grid.SetGridSizeAsync(combatId, width, height);
    public Task<Combat> SetParticipantPositionAsync(Guid combatId, Guid participantId, int gridX, int gridY)
        => _grid.SetParticipantPositionAsync(combatId, participantId, gridX, gridY);
    public Task<Combat> MoveParticipantAsync(Guid combatId, Guid participantId, int newGridX, int newGridY)
        => _grid.MoveParticipantAsync(combatId, participantId, newGridX, newGridY);
    public Task<GridPosition?> GetParticipantPositionAsync(Guid combatId, Guid participantId)
        => _grid.GetParticipantPositionAsync(combatId, participantId);
    public Task<List<GridPosition>> GetAdjacentPositionsAsync(Guid combatId, int gridX, int gridY, int range = 1)
        => _grid.GetAdjacentPositionsAsync(combatId, gridX, gridY, range);

    // ===== AI =====
    public Task<AICombatSuggestion> GetAITacticalSuggestionsAsync(Guid combatId)
        => _ai.GetAITacticalSuggestionsAsync(combatId);
    public Task<AICombatSuggestion> GetAINPCBehaviorAsync(Guid combatId, Guid npcId)
        => _ai.GetAINPCBehaviorAsync(combatId, npcId);
    public Task<Combat> AutoResolveCombatAsync(Guid combatId, string resolutionMode = "quick")
        => _ai.AutoResolveCombatAsync(combatId, resolutionMode);

    // ===== SAN =====
    public Task<Combat> ApplySANLossAsync(Guid combatId, Guid participantId, int sanLoss, string reason)
        => _san.ApplySANLossAsync(combatId, participantId, sanLoss, reason);
    public Task<Combat> ApplySANRecoveryAsync(Guid combatId, Guid participantId, int sanRecovery)
        => _san.ApplySANRecoveryAsync(combatId, participantId, sanRecovery);
    public Task<SanityCheckResult> MakeSANCheckAsync(Guid combatId, Guid participantId, int dc)
        => _san.MakeSANCheckAsync(combatId, participantId, dc);

    // ===== Queries =====
    public Task<CombatLog> GetCombatLogAsync(Guid combatId) => _query.GetCombatLogAsync(combatId);
    public Task<Combat?> GetCombatAsync(Guid combatId) => _query.GetCombatAsync(combatId);
    public Task<List<Combat>> GetActiveCombatAsync(Guid gameId) => _query.GetActiveCombatAsync(gameId);

    // ===== Private Helpers =====
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

    private Dictionary<string, int> GetSavingThrows(CombatParticipant participant)
    {
        if (participant.SavingThrows.HasValue &&
            participant.SavingThrows.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(participant.SavingThrows.ToString())
                ?? new Dictionary<string, int>();
        }
        return new Dictionary<string, int>();
    }

    // ===== Action Economy =====
    public async Task<Combat> SpendActionAsync(Guid combatId, Guid participantId)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        if (participant.ActionsRemaining <= 0)
            throw new InvalidOperationException("No actions remaining.");

        participant.ActionsRemaining--;
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, participant.DisplayName, "Action",
            $"Used 1 action ({participant.ActionsRemaining} remaining)");

        return combat;
    }

    public async Task<Combat> SpendBonusActionAsync(Guid combatId, Guid participantId)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        if (participant.BonusActionsRemaining <= 0)
            throw new InvalidOperationException("No bonus actions remaining.");

        participant.BonusActionsRemaining--;
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, participant.DisplayName, "Bonus Action",
            $"Used 1 bonus action ({participant.BonusActionsRemaining} remaining)");

        return combat;
    }

    public async Task<Combat> SpendReactionAsync(Guid combatId, Guid participantId)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        if (participant.ReactionsRemaining <= 0)
            throw new InvalidOperationException("No reactions remaining.");

        participant.ReactionsRemaining--;
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, participant.DisplayName, "Reaction",
            $"Used 1 reaction ({participant.ReactionsRemaining} remaining)");

        return combat;
    }

    public async Task<Combat> SpendMovementAsync(Guid combatId, Guid participantId)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        if (participant.MovementsRemaining <= 0)
            throw new InvalidOperationException("No movements remaining.");

        participant.MovementsRemaining--;
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, participant.DisplayName, "Movement",
            $"Used 1 movement ({participant.MovementsRemaining} remaining)");

        return combat;
    }

    public async Task<Combat> RefreshActionsAsync(Guid combatId, Guid participantId)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        // D&D 5e defaults: 1 action, 0 bonus actions, 1 reaction per turn
        participant.ActionsRemaining = 1;
        participant.BonusActionsRemaining = 0;
        participant.ReactionsRemaining = 1;
        participant.MovementsRemaining = 1;
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, participant.DisplayName, "System",
            "Actions refreshed for new turn");

        return combat;
    }

    public async Task<Combat> SetActionsAsync(Guid combatId, Guid participantId, int actions, int bonusActions, int reactions, int movements)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        participant.ActionsRemaining = actions;
        participant.BonusActionsRemaining = bonusActions;
        participant.ReactionsRemaining = reactions;
        participant.MovementsRemaining = movements;
        await _context.SaveChangesAsync();

        return combat;
    }

    public async Task<CombatParticipant> GetActionEconomyAsync(Guid combatId, Guid participantId)
    {
        var combat = await _query.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        return participant;
    }
}
