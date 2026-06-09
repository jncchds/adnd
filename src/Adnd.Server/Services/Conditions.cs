using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Conditions ====================

    public async Task<Combat> ApplyConditionAsync(Guid combatId, Guid participantId, string conditionName,
        int? duration = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);

        // Check if condition already exists - refresh duration if so
        var existingIdx = conditions.FindIndex(c => c.Name.ToLower() == conditionName.ToLower());
        if (existingIdx >= 0)
        {
            if (duration.HasValue && duration.Value > 0)
            {
                conditions[existingIdx].Duration = duration.Value;
                conditions[existingIdx].Description = description;
            }
        }
        else
        {
            conditions.Add(new ConditionEntry
            {
                Name = conditionName,
                Duration = duration ?? 0,
                Description = description
            });
        }

        participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Applied '{conditionName}'{(duration.HasValue && duration.Value > 0 ? $" for {duration.Value} round(s)" : "")}.");

        return combat;
    }

    public async Task<Combat> RemoveConditionAsync(Guid combatId, Guid participantId, string conditionName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);
        var removed = conditions.RemoveAll(c => c.Name.ToLower() == conditionName.ToLower());

        if (removed > 0)
        {
            participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
            _context.Combats.Update(combat);
            await _context.SaveChangesAsync();

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Removed '{conditionName}'.");
        }

        return combat;
    }

    public async Task<Combat> ClearConditionsAsync(Guid combatId, Guid participantId, string? exceptCondition = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);
        var cleared = exceptCondition == null
            ? conditions.Count
            : conditions.RemoveAll(c => c.Name.ToLower() != exceptCondition.ToLower());

        if (cleared > 0)
        {
            participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
            _context.Combats.Update(combat);
            await _context.SaveChangesAsync();

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Cleared {(cleared == conditions.Count + cleared ? "all conditions" : $"{cleared} conditions")}.");
        }

        return combat;
    }

}
