using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

public sealed class CombatSpellService(AppDbContext db, CombatEventLogger logger) : ICombatSpellService
{
    public async Task CastSpellAsync(
        Guid participantId,
        string spellName,
        int spellLevel,
        CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        if (spellLevel > 0 && participant.CharacterId.HasValue)
            await ConsumeSpellSlotCoreAsync(participant.CharacterId.Value, spellLevel, ct);

        logger.Log(
            participant.Combat,
            CombatEventType.Attack,
            participantId,
            null,
            $"Cast {spellName} (level {spellLevel})",
            new { spellName, spellLevel });

        await db.SaveChangesAsync(ct);
    }

    public async Task ConsumeSpellSlotAsync(Guid participantId, int level, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants.FindAsync([participantId], ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        if (!participant.CharacterId.HasValue)
            return;

        await ConsumeSpellSlotCoreAsync(participant.CharacterId.Value, level, ct);
        await db.SaveChangesAsync(ct);
    }

    // Decrements the remaining count for the given spell slot level in Character.SpellSlots jsonb.
    // Expected jsonb format: {"1": {"total": 4, "remaining": 3}, "2": {"total": 2, "remaining": 1}}
    private async Task ConsumeSpellSlotCoreAsync(Guid characterId, int level, CancellationToken ct)
    {
        var character = await db.Characters.FindAsync([characterId], ct);
        if (character is null) return;

        var slots = character.SpellSlots.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, SpellSlotLevel>>(character.SpellSlots.GetRawText(), SlotJson)
              ?? new()
            : new Dictionary<string, SpellSlotLevel>();

        var key = level.ToString();
        if (slots.TryGetValue(key, out var slot) && slot.Remaining > 0)
        {
            slots[key] = slot with { Remaining = slot.Remaining - 1 };
            character.SpellSlots = JsonSerializer.SerializeToElement(slots, SlotJson);
        }
    }

    /// <summary>
    /// Slots are stored camelCase ({"1":{"total":4,"remaining":3}}) but the record's
    /// parameters are PascalCase. JsonSerializerOptions.Default is case-SENSITIVE, so
    /// without this every slot deserialized to (0, 0): the remaining-count check never
    /// passed and casters effectively had unlimited spells.
    /// </summary>
    private static readonly JsonSerializerOptions SlotJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private record SpellSlotLevel(int Total, int Remaining);
}
