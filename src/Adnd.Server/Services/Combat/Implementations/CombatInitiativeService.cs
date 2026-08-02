using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

public sealed class CombatInitiativeService(AppDbContext db, IDiceEngine diceEngine) : ICombatInitiativeService
{
    public async Task<CombatParticipant> RollInitiativeAsync(
        Guid participantId,
        int dexModifier,
        CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants.FindAsync([participantId], ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var roll = diceEngine.Roll("1d20");
        participant.Initiative = roll.Total + dexModifier;
        await db.SaveChangesAsync(ct);
        return participant;
    }

    public async Task RollInitiativeForAllAsync(Guid combatId, CancellationToken ct = default)
    {
        var participants = await db.CombatParticipants
            .Where(p => p.CombatId == combatId)
            .ToListAsync(ct);

        // Fetch every participant's character in one round trip. This used to issue a
        // FindAsync per participant inside the loop.
        var characterIds = participants
            .Where(p => p.CharacterId.HasValue)
            .Select(p => p.CharacterId!.Value)
            .Distinct()
            .ToList();

        var dexModifiers = characterIds.Count == 0
            ? []
            : await db.Characters
                .AsNoTracking()
                .Where(c => characterIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Attributes })
                .ToDictionaryAsync(c => c.Id, c => ExtractDexModifier(c.Attributes), ct);

        foreach (var p in participants)
        {
            var roll = diceEngine.Roll("1d20");
            var dexMod = p.CharacterId.HasValue && dexModifiers.TryGetValue(p.CharacterId.Value, out var mod)
                ? mod
                : 0;

            p.Initiative = roll.Total + dexMod;
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<List<CombatParticipant>> GetInitiativeOrderAsync(Guid combatId, CancellationToken ct = default)
        => db.CombatParticipants
            .Where(p => p.CombatId == combatId)
            .OrderByDescending(p => p.Initiative)
            .ToListAsync(ct);

    // Derives the DEX ability modifier from the attributes jsonb.
    // Expects: {"dex": 14} or {"DEX": 14} or {"dexterity": 14}.
    // Modifier = (score - 10) / 2, rounded down.
    private static int ExtractDexModifier(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object) return 0;

        foreach (var prop in attributes.EnumerateObject())
        {
            if (prop.Name.StartsWith("dex", StringComparison.OrdinalIgnoreCase) &&
                prop.Value.TryGetInt32(out var score))
            {
                return (int)Math.Floor((score - 10) / 2.0);
            }
        }

        return 0;
    }
}
