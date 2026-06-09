using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== SAN (CoC Specific) ====================

    public async Task<Combat> ApplySANLossAsync(Guid combatId, Guid participantId, int sanLoss, string reason)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Track SAN in participant notes
        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);
        var newSAN = Math.Max(0, currentSAN - sanLoss);
        notes["currentSAN"] = newSAN;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"SAN loss: {currentSAN} -> {newSAN} ({sanLoss} lost) - {reason}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> ApplySANRecoveryAsync(Guid combatId, Guid participantId, int sanRecovery)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);
        var newSAN = Math.Min(maxSAN, currentSAN + sanRecovery);
        notes["currentSAN"] = newSAN;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, "System", participant.DisplayName,
            $"SAN recovery: {currentSAN} -> {newSAN} (+{sanRecovery})");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<SanityCheckResult> MakeSANCheckAsync(Guid combatId, Guid participantId, int dc)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);

        var rollResult = _diceEngine.Roll("d100");
        var roll = rollResult.Total;
        var success = roll <= currentSAN;
        var isCritical = roll <= (currentSAN / 5);
        var isFumble = roll >= 95;

        int sanLoss = 0;
        string effect = string.Empty;

        if (success)
        {
            if (isCritical)
            {
                sanLoss = 0;
                effect = "Critical success - no SAN loss";
            }
            else if (isFumble)
            {
                var fumbleDmg = _diceEngine.Roll("1d10");
                sanLoss = fumbleDmg.Total;
                effect = $"Fumble! Lost {sanLoss} SAN";
            }
            else
            {
                sanLoss = 0;
                effect = "Success - no SAN loss";
            }
        }
        else
        {
            var sanDmg = _diceEngine.Roll("1d10");
            sanLoss = sanDmg.Total;
            effect = $"Failed - lost {sanLoss} SAN";
        }

        var newSAN = Math.Max(0, currentSAN - sanLoss);
        notes["currentSAN"] = newSAN;
        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, participant.DisplayName, participant.DisplayName,
            $"SAN check: d100={roll} vs {currentSAN} -> {(success ? "SUCCESS" : "FAILURE")} | SAN: {currentSAN} -> {newSAN} (-{sanLoss})");

        return new SanityCheckResult
        {
            Participant = participant.DisplayName,
            CurrentSAN = currentSAN,
            Roll = roll,
            DC = dc,
            Success = success,
            IsCritical = isCritical,
            SANLoss = sanLoss,
            Effect = effect,
            RolledAt = DateTime.UtcNow
        };
    }

}
