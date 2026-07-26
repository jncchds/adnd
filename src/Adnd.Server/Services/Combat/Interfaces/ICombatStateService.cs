using Adnd.Server.Models;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public interface ICombatStateService
{
    Task<CombatEntity?> GetCombatAsync(Guid combatId, CancellationToken ct = default);
    Task<CombatParticipant?> GetCurrentParticipantAsync(Guid combatId, CancellationToken ct = default);
    Task<bool> IsCombatActiveAsync(Guid gameId, CancellationToken ct = default);
}
