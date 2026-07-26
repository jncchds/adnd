using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public sealed class CombatLifecycleService(AppDbContext db, CombatEventLogger logger) : ICombatLifecycleService
{
    public async Task<CombatEntity> StartCombatAsync(
        Guid gameId,
        Guid sessionId,
        string name,
        CancellationToken ct = default)
    {
        var combat = new CombatEntity
        {
            GameId = gameId,
            SessionId = sessionId,
            Name = name,
            Status = CombatStatus.Active,
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Combats.Add(combat);
        logger.Log(combat, CombatEventType.Start, null, null, $"Combat started: {name}");
        await db.SaveChangesAsync(ct);
        return combat;
    }

    public async Task EndCombatAsync(Guid combatId, CancellationToken ct = default)
    {
        var combat = await db.Combats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == combatId, ct);

        if (combat is null) return;

        combat.Status = CombatStatus.Finished;
        combat.UpdatedAt = DateTimeOffset.UtcNow;
        logger.Log(combat, CombatEventType.End, null, null, "Combat ended");
        await db.SaveChangesAsync(ct);
    }

    public Task<CombatEntity?> GetActiveCombatAsync(Guid gameId, CancellationToken ct = default)
        => db.Combats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.GameId == gameId && c.Status == CombatStatus.Active, ct);
}
