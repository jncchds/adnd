using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public interface ICombatTurnService
{
    Task<CombatEntity> AdvanceTurnAsync(Guid combatId, CancellationToken ct = default);
    Task<CombatEntity> RetreatTurnAsync(Guid combatId, CancellationToken ct = default);
    Task ResetActionEconomyAsync(Guid participantId, CancellationToken ct = default);
}
