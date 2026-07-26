namespace Adnd.Server.Services.Combat;

/// <summary>
/// Facade interface for all combat domain services.
/// Sub-services handle specific concerns; the facade exposes convenience methods
/// for the most common operations (damage, healing, conditions, death saves).
/// </summary>
public interface ICombatService
{
    ICombatLifecycleService Lifecycle { get; }
    ICombatParticipantService Participants { get; }
    ICombatInitiativeService Initiative { get; }
    ICombatTurnService Turns { get; }
    ICombatStateService State { get; }
    ICombatSpellService Spells { get; }
    ICombatInventoryService Inventory { get; }
    ICombatProgressionService Progression { get; }
    ICombatGridService Grid { get; }
    ICombatAIService AI { get; }
    ICombatQueryService Query { get; }
    ISANService SAN { get; }

    Task<DamageResult> DealDamageAsync(Guid participantId, int amount, string damageType, CancellationToken ct = default);
    Task HealAsync(Guid participantId, int amount, CancellationToken ct = default);
    Task ApplyConditionAsync(Guid participantId, string condition, CancellationToken ct = default);
    Task RemoveConditionAsync(Guid participantId, string condition, CancellationToken ct = default);
    Task<DeathSaveResult> RecordDeathSaveAsync(Guid participantId, bool isSuccess, int roll, CancellationToken ct = default);
}
