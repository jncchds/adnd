using Adnd.Server.Models;

namespace Adnd.Server.Services.Combat;

public interface ICombatParticipantService
{
    Task<CombatParticipant> AddParticipantAsync(
        Guid combatId,
        string displayName,
        CombatParticipantType type,
        Guid? characterId = null,
        Guid? npcId = null,
        Guid? playerId = null,
        CancellationToken ct = default);

    Task RemoveParticipantAsync(Guid participantId, CancellationToken ct = default);
    Task<List<CombatParticipant>> GetParticipantsAsync(Guid combatId, CancellationToken ct = default);
}
