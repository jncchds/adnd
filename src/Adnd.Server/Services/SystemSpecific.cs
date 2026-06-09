using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== System-Specific Rules ====================

    public async Task<Combat> ApplySystemSpecificEffectsAsync(Guid combatId, string systemId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);

        switch (systemId)
        {
            case "dnd5e":
                // Apply exhaustion levels if applicable
                var exhaustion = conditions.FirstOrDefault(c => c.Name.ToLower() == "exhaustion");
                if (exhaustion != null && exhaustion.Duration > 0)
                {
                    // Reduce exhaustion duration
                    exhaustion.Duration--;
                    if (exhaustion.Duration <= 0)
                    {
                        conditions.Remove(exhaustion);
                        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                            CombatEventType.Condition, "System", participant.DisplayName,
                            "Exhaustion level removed.");
                    }
                }
                break;

            case "coc7e":
                // CoC: Check for SAN loss from combat
                // This would be triggered by specific events
                break;

            case "pf2e":
                // PF2e: Handle conditions specific to Pathfinder
                break;
        }

        participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> CalculateProficiencyBonusAsync(Guid combatId, string systemId, int level)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        int proficiencyBonus = 0;

        if (systemId == "dnd5e")
        {
            // D&D 5e proficiency progression
            if (level <= 4) proficiencyBonus = 2;
            else if (level <= 8) proficiencyBonus = 3;
            else if (level <= 12) proficiencyBonus = 4;
            else if (level <= 16) proficiencyBonus = 5;
            else proficiencyBonus = 6;
        }
        else if (systemId == "pf2e")
        {
            // PF2e proficiency progression
            proficiencyBonus = 2 + (level - 1) / 2;
        }
        // CoC doesn't use proficiency bonus

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            $"Proficiency bonus for level {level}: {proficiencyBonus}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> CalculateSavingThrowAsync(Guid combatId, string systemId, string saveType,
        Guid participantId, int? proficiencyBonus = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var savingThrows = GetSavingThrows(participant);
        var baseSave = savingThrows.ContainsKey(saveType.ToLower()) ? savingThrows[saveType.ToLower()] : 0;

        string result;
        if (systemId == "dnd5e")
        {
            var prof = proficiencyBonus ?? 2;
            result = $"{saveType}: {baseSave} + {prof} = {baseSave + prof}";
        }
        else if (systemId == "pf2e")
        {
            var prof = proficiencyBonus ?? 2;
            result = $"{saveType}: {baseSave} + {prof} = {baseSave + prof}";
        }
        else
        {
            result = $"{saveType}: {baseSave}";
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, "System", participant.DisplayName,
            result);

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
