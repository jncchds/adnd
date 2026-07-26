using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public sealed class CombatStateService(AppDbContext db) : ICombatStateService
{
    public Task<CombatEntity?> GetCombatAsync(Guid combatId, CancellationToken ct = default)
        => db.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId, ct);

    public async Task<CombatParticipant?> GetCurrentParticipantAsync(
        Guid combatId,
        CancellationToken ct = default)
    {
        var combat = await db.Combats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == combatId, ct);

        if (combat is null) return null;

        var ordered = combat.Participants
            .OrderByDescending(p => p.Initiative)
            .ToList();

        if (ordered.Count == 0 || combat.CurrentTurnIndex >= ordered.Count)
            return null;

        return ordered[combat.CurrentTurnIndex];
    }

    public async Task<bool> IsCombatActiveAsync(Guid gameId, CancellationToken ct = default)
        => await db.Combats
            .AnyAsync(c => c.GameId == gameId && c.Status == CombatStatus.Active, ct);
}
