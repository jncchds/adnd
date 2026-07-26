using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public sealed class CombatTurnService(AppDbContext db, CombatEventLogger logger) : ICombatTurnService
{
    public async Task<CombatEntity> AdvanceTurnAsync(Guid combatId, CancellationToken ct = default)
    {
        var combat = await db.Combats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == combatId, ct)
            ?? throw new InvalidOperationException($"Combat {combatId} not found.");

        var count = Math.Max(combat.Participants.Count, 1);
        combat.CurrentTurnIndex = (combat.CurrentTurnIndex + 1) % count;

        if (combat.CurrentTurnIndex == 0)
        {
            combat.CurrentRound++;
            logger.Log(combat, CombatEventType.RoundStart, null, null, $"Round {combat.CurrentRound} started");
        }

        combat.UpdatedAt = DateTimeOffset.UtcNow;
        logger.Log(combat, CombatEventType.TurnChange, null, null, $"Turn {combat.CurrentTurnIndex}");
        await db.SaveChangesAsync(ct);
        return combat;
    }

    public async Task<CombatEntity> RetreatTurnAsync(Guid combatId, CancellationToken ct = default)
    {
        var combat = await db.Combats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == combatId, ct)
            ?? throw new InvalidOperationException($"Combat {combatId} not found.");

        combat.CurrentTurnIndex = Math.Max(0, combat.CurrentTurnIndex - 1);
        combat.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return combat;
    }

    public async Task ResetActionEconomyAsync(Guid participantId, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants.FindAsync([participantId], ct);
        if (participant is null) return;

        participant.ActionsRemaining = 1;
        participant.BonusActionsRemaining = 1;
        participant.ReactionsRemaining = 1;
        participant.MovementsRemaining = 30;
        participant.FreeActions = 0;
        await db.SaveChangesAsync(ct);
    }
}
