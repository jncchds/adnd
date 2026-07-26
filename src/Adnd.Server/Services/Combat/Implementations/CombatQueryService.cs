using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

public sealed class CombatQueryService(AppDbContext db) : ICombatQueryService
{
    public Task<List<CombatEvent>> GetCombatHistoryAsync(Guid combatId, CancellationToken ct = default)
        => db.CombatEvents
            .Where(e => e.CombatId == combatId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

    public Task<CombatParticipant?> GetParticipantAsync(Guid participantId, CancellationToken ct = default)
        => db.CombatParticipants.FindAsync([participantId], ct).AsTask();
}
