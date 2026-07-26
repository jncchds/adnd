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

        foreach (var p in participants)
        {
            var roll = diceEngine.Roll("1d20");
            int dexMod = 0;

            // For character participants, look up their DEX modifier from Attributes jsonb
            if (p.CharacterId.HasValue)
            {
                var character = await db.Characters.FindAsync([p.CharacterId.Value], ct);
                if (character is not null)
                    dexMod = ExtractDexModifier(character.Attributes);
            }

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
