using Adnd.Server.Models;

namespace Adnd.Server.Services.Combat;

public interface ICombatQueryService
{
    Task<List<CombatEvent>> GetCombatHistoryAsync(Guid combatId, CancellationToken ct = default);
    Task<CombatParticipant?> GetParticipantAsync(Guid participantId, CancellationToken ct = default);
}
