using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== HP Management ====================

    public async Task<Combat> DealDamageAsync(Guid combatId, Guid participantId, int damage, string? source = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        return await DealDamageToParticipant(combat, participant, damage, source ?? "unknown");
    }

    public async Task<Combat> DealTemporaryHPAsync(Guid combatId, Guid participantId, int tempHP)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var currentTemp = participant.TemporaryHP?.GetInt32() ?? 0;
        participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(currentTemp + tempHP)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, "System", participant.DisplayName,
            $"Gained {tempHP} temporary HP (total: {currentTemp + tempHP}).");

        return combat;
    }

    public async Task<Combat> HealAsync(Guid combatId, Guid participantId, int amount, string? source = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var oldHP = participant.CurrentHP;
        participant.CurrentHP = Math.Min(participant.MaxHP, participant.CurrentHP + amount);
        var actualHeal = participant.CurrentHP - oldHP;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, source ?? "System", participant.DisplayName,
            $"Healed {actualHeal} HP ({oldHP} -> {participant.CurrentHP}/{participant.MaxHP}).");

        return combat;
    }

}
