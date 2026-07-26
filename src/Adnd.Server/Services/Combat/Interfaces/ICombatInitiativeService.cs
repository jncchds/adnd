using Adnd.Server.Models;

namespace Adnd.Server.Services.Combat;

public interface ICombatInitiativeService
{
    Task<CombatParticipant> RollInitiativeAsync(
        Guid participantId,
        int dexModifier,
        CancellationToken ct = default);

    Task RollInitiativeForAllAsync(Guid combatId, CancellationToken ct = default);
    Task<List<CombatParticipant>> GetInitiativeOrderAsync(Guid combatId, CancellationToken ct = default);
}
