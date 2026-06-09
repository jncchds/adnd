using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Rest System ====================

    public async Task<Combat> StartShortRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Name = combat.Name + " (Short Rest)";

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Short rest started. Participants can recover HP and resources.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> StartLongRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Name = combat.Name + " (Long Rest)";

        // Recover all HP
        foreach (var participant in combat.Participants)
        {
            if (participant.CurrentHP < participant.MaxHP)
            {
                var oldHP = participant.CurrentHP;
                participant.CurrentHP = participant.MaxHP;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Healing, "System", participant.DisplayName,
                    $"Long rest recovery: {oldHP} -> {participant.CurrentHP} HP");
            }
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Long rest completed. All HP recovered.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> EndRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Rest ended. Combat may resume.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<RestResult> GetCurrentRestStatusAsync(Guid combatId)
    {
        var combat = await GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        return new RestResult
        {
            RestType = combat.Name?.Contains("Short") == true ? "Short" :
                       combat.Name?.Contains("Long") == true ? "Long" : "None",
            IsInProgress = combat.Name?.Contains("Rest") == true,
            HPRecovered = combat.Participants.Sum(p => Math.Max(0, p.MaxHP - p.CurrentHP)),
            Effects = new List<string> { "HP recovery", "Resource recovery" }
        };
    }

}
