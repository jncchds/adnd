using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

public sealed class CombatParticipantService(AppDbContext db) : ICombatParticipantService
{
    public async Task<CombatParticipant> AddParticipantAsync(
        Guid combatId,
        string displayName,
        CombatParticipantType type,
        Guid? characterId = null,
        Guid? npcId = null,
        Guid? playerId = null,
        CancellationToken ct = default)
    {
        var participant = new CombatParticipant
        {
            CombatId = combatId,
            DisplayName = displayName,
            ParticipantType = type,
            CharacterId = characterId,
            NPCId = npcId,
            PlayerId = playerId,
            // Default action economy (D&D 5e)
            ActionsRemaining = 1,
            BonusActionsRemaining = 1,
            ReactionsRemaining = 1,
            MovementsRemaining = 30,
            FreeActions = 0
        };

        db.CombatParticipants.Add(participant);
        await db.SaveChangesAsync(ct);
        return participant;
    }

    public async Task RemoveParticipantAsync(Guid participantId, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants.FindAsync([participantId], ct);
        if (participant is null) return;

        db.CombatParticipants.Remove(participant);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<CombatParticipant>> GetParticipantsAsync(Guid combatId, CancellationToken ct = default)
        => db.CombatParticipants
            .Where(p => p.CombatId == combatId)
            .OrderByDescending(p => p.Initiative)
            .ToListAsync(ct);
}
