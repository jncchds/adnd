using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Character Progression ====================

    public async Task<Combat> AddXPAsync(Guid combatId, Guid participantId, int xpAmount, string reason)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Track XP in participant notes (JSON)
        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("xp")) notes["xp"] = 0;
        notes["xp"] = (int)notes["xp"] + xpAmount;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Revival, participant.DisplayName, participant.DisplayName,
            $"Gained {xpAmount} XP ({reason}). Total: {notes["xp"]}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> LevelUpAsync(Guid combatId, Guid participantId, int newLevel, string systemId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var oldLevel = participant.MaxHP; // Using MaxHP field to track level for now
        participant.MaxHP = newLevel;

        // Calculate new HP based on system
        int newMaxHP;
        if (systemId == "dnd5e")
        {
            // Simplified: +2 HP per level
            newMaxHP = participant.CurrentHP + 2;
        }
        else if (systemId == "pf2e")
        {
            // PF2e: +6 HP + CON mod per level
            newMaxHP = participant.CurrentHP + 6;
        }
        else
        {
            newMaxHP = participant.CurrentHP + 2;
        }

        participant.CurrentHP = newMaxHP;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Revival, participant.DisplayName, participant.DisplayName,
            $"Levelled up! {oldLevel} -> {newLevel}. HP: {participant.CurrentHP}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> CalculateXPForCombatAsync(Guid combatId, string systemId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        // Calculate XP based on defeated NPCs
        var defeatedNPCs = combat.Participants.Where(p =>
            p.ParticipantType == "NPC" && p.CurrentHP <= 0).ToList();

        int totalXP = 0;
        foreach (var npc in defeatedNPCs)
        {
            // Simplified XP calculation
            var xp = 100; // Base XP per NPC
            totalXP += xp;

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Revival, "System", npc.DisplayName,
                $"Defeated NPC awarded {xp} XP.");
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            $"Total XP from combat: {totalXP} ({defeatedNPCs.Count} NPCs)");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
