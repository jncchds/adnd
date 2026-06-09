using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Turn Management ====================

    public async Task<Combat> AdvanceTurnAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Participants.Count == 0)
            throw new InvalidOperationException("No participants in combat.");

        var oldTurnIndex = combat.CurrentTurnIndex;
        combat.CurrentTurnIndex++;

        if (combat.CurrentTurnIndex >= combat.Participants.Count)
        {
            // New round
            combat.CurrentRound++;
            combat.CurrentTurnIndex = 0;

            // Process end-of-round effects on conditions
            foreach (var participant in combat.Participants)
            {
                var conditions = await GetConditions(participant);
                // Reduce duration on conditions with duration > 0
                var updatedConditions = new List<ConditionEntry>();
                foreach (var cond in conditions)
                {
                    if (cond.Duration > 0)
                    {
                        cond.Duration--;
                        if (cond.Duration > 0)
                            updatedConditions.Add(cond);
                        else
                        {
                            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                                CombatEventType.Condition, "System", participant.DisplayName,
                                $"Condition '{cond.Name}' expired.");
                        }
                    }
                }
                participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(updatedConditions)).RootElement;
            }
        }

        var newTurnParticipant = combat.Participants[combat.CurrentTurnIndex];

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.TurnChange, "System", "System",
            $"Round {combat.CurrentRound}, Turn: {newTurnParticipant.DisplayName}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> RetreatTurnAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.CurrentTurnIndex > 0)
        {
            combat.CurrentTurnIndex--;
        }
        else if (combat.CurrentRound > 1)
        {
            combat.CurrentRound--;
            combat.CurrentTurnIndex = combat.Participants.Count - 1;
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<CombatParticipant> GetCurrentTurnParticipantAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Participants.Count == 0)
            throw new InvalidOperationException("No participants in combat.");

        return combat.Participants[combat.CurrentTurnIndex];
    }

    public async Task<Combat> SetCurrentTurnAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var idx = combat.Participants.FindIndex(p => p.Id == participantId);
        if (idx < 0)
            throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        combat.CurrentTurnIndex = idx;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
