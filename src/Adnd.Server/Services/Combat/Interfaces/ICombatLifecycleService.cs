using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public interface ICombatLifecycleService
{
    Task<CombatEntity> StartCombatAsync(Guid gameId, Guid sessionId, string name, CancellationToken ct = default);
    Task EndCombatAsync(Guid combatId, CancellationToken ct = default);
    Task<CombatEntity?> GetActiveCombatAsync(Guid gameId, CancellationToken ct = default);
}
