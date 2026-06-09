using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Initiative ====================

    public async Task<(CombatParticipant participant, int[] rolls)> RollInitiativeAsync(Guid combatId, Guid participantId, string formula = "1d20")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var result = _diceEngine.Roll(formula);
        participant.Initiative = result.Total;
        participant.InitiativeCount = combat.InitiativeCount++;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Initiative, participant.DisplayName, participant.DisplayName,
            $"Initiative: {formula} = {result.Total} ({string.Join(',', result.Rolls)})");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        return (participant, result.Rolls);
    }

    public async Task<Combat> RollInitiativeForAllAsync(Guid combatId, string formula = "1d20")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        foreach (var participant in combat.Participants)
        {
            var result = _diceEngine.Roll(formula);
            participant.Initiative = result.Total;
            participant.InitiativeCount = combat.InitiativeCount++;

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Initiative, participant.DisplayName, participant.DisplayName,
                $"Initiative: {formula} = {result.Total} ({string.Join(',', result.Rolls)})");
        }

        // Sort by initiative (desc), then by initiativeCount (asc) for tiebreak
        combat.Participants = combat.Participants
            .OrderByDescending(p => p.Initiative)
            .ThenBy(p => p.InitiativeCount)
            .ToList();

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        // Build turn order string
        var turnOrder = string.Join(", ", combat.Participants.Select(p => $"{p.DisplayName} ({p.Initiative})"));
        await AddCombatEvent(combat, combat.CurrentRound, 0, CombatEventType.RoundStart, "System", "System",
            $"Initiative order: {turnOrder}");

        return combat;
    }

    public async Task<Combat> ReorderInitiativeAsync(Guid combatId, List<Guid> participantIdsInOrder)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Participants = participantIdsInOrder
            .Select(id => combat.Participants.First(p => p.Id == id))
            .ToList();

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
