using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

public sealed class CombatProgressionService(AppDbContext db, CombatEventLogger logger) : ICombatProgressionService
{
    /// <summary>
    /// Records an XP grant event.
    /// XP is not stored per-participant in this schema — just log the event.
    /// </summary>
    public async Task GrantXPAsync(Guid participantId, int amount, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct);

        if (participant is null) return;

        logger.Log(
            participant.Combat,
            CombatEventType.Revival,   // closest semantic match for "positive change"
            participantId,
            null,
            $"XP granted: {amount}",
            new { xp = amount });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Increments Character.Level and recalculates ProficiencyBonus per D&amp;D 5e table.
    /// </summary>
    public async Task<Character?> LevelUpAsync(Guid participantId, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants.FindAsync([participantId], ct);
        if (participant?.CharacterId is null) return null;

        var character = await db.Characters.FindAsync([participant.CharacterId.Value], ct);
        if (character is null) return null;

        character.Level = Math.Min(20, character.Level + 1);
        character.ProficiencyBonus = CalcProfBonus(character.Level);
        await db.SaveChangesAsync(ct);
        return character;
    }

    // D&D 5e proficiency bonus table
    private static int CalcProfBonus(int level) =>
        level switch
        {
            <= 4 => 2,
            <= 8 => 3,
            <= 12 => 4,
            <= 16 => 5,
            _ => 6
        };
}
