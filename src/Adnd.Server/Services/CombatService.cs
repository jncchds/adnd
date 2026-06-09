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

public partial class CombatService : ICombatService
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

}
