using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Participants ====================

    public async Task<CombatParticipant> AddParticipantAsync(Guid combatId, string participantType,
        Guid? playerId, Guid? npcId, string displayName, int ac, int currentHP, int maxHP,
        JsonElement? conditions = null, JsonElement? savingThrows = null,
        JsonElement? deathSaveState = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Status != CombatStatus.Active)
            throw new InvalidOperationException("Cannot add participants to a finished combat.");

        var participant = new CombatParticipant
        {
            CombatId = combatId,
            ParticipantType = participantType,
            PlayerId = playerId,
            NpcId = npcId,
            DisplayName = displayName,
            AC = ac,
            CurrentHP = currentHP,
            MaxHP = maxHP,
            Conditions = conditions ?? JsonDocument.Parse("[]").RootElement,
            SavingThrows = savingThrows,
            DeathSaveState = deathSaveState,
            InitiativeCount = combat.InitiativeCount++
        };

        _context.CombatParticipants.Add(participant);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatStart, participant.DisplayName, participant.DisplayName,
            $"Added to combat (HP: {currentHP}/{maxHP}, AC: {ac}).");

        return participant;
    }

    public async Task<Combat> RemoveParticipantAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null)
            throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        _context.CombatParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatEnd, participant.DisplayName, participant.DisplayName,
            "Removed from combat.");

        // Adjust turn index if needed
        if (combat.CurrentTurnIndex >= combat.Participants.Count)
            combat.CurrentTurnIndex = Math.Max(0, combat.Participants.Count - 1);

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
