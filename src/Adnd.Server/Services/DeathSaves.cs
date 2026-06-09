using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Death Saves ====================

    public async Task<DeathSaveResult> MakeDeathSaveAsync(Guid combatId, Guid participantId, bool success)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);

        if (success)
        {
            deathState.Successes++;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.DeathSave, participant.DisplayName, participant.DisplayName,
                $"Death save SUCCESS ({deathState.Successes}/3 successes).");

            if (deathState.Successes >= 3)
            {
                participant.CurrentHP = 1;
                participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Revival, "System", participant.DisplayName,
                    "Stabilized! Revived to 1 HP.");
            }
        }
        else
        {
            deathState.Failures++;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.DeathSave, participant.DisplayName, participant.DisplayName,
                $"Death save FAILURE ({deathState.Failures}/3 failures).");

            if (deathState.Failures >= 3)
            {
                participant.CurrentHP = 0;
                participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Death, participant.DisplayName, participant.DisplayName,
                    "DIED from death saves!");
            }
        }

        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        return new DeathSaveResult
        {
            Participant = participant.DisplayName,
            Success = success,
            Successes = deathState.Successes,
            Failures = deathState.Failures,
            IsStabilized = deathState.Successes >= 3,
            IsDead = deathState.Failures >= 3,
            RolledAt = DateTime.UtcNow
        };
    }

    public async Task<Combat> AddDeathSaveSuccessAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);
        deathState.Successes++;
        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        if (deathState.Successes >= 3)
        {
            participant.CurrentHP = 1;
            participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Revival, "System", participant.DisplayName,
                "Stabilized! Revived to 1 HP.");
        }

        return combat;
    }

    public async Task<Combat> AddDeathSaveFailureAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);
        deathState.Failures++;
        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        if (deathState.Failures >= 3)
        {
            participant.CurrentHP = 0;
            participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Death, participant.DisplayName, participant.DisplayName,
                "DIED from death saves!");
        }

        return combat;
    }

}
