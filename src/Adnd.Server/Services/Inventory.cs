using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Inventory/Equipment ====================

    public async Task<Combat> AddItemToParticipantAsync(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Track inventory in participant notes
        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("inventory")) notes["inventory"] = new List<object>();

        var invList = JsonSerializer.Deserialize<List<object>>(notes["inventory"]?.ToString() ?? "[]") ?? new List<object>();
        invList.Add(new { Name = itemName, Type = itemType, Quantity = quantity, Stats = itemStats });
        notes["inventory"] = invList;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Added {quantity}x {itemName} ({itemType}).");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> RemoveItemFromParticipantAsync(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes != null && notes.ContainsKey("inventory"))
        {
            var invList = JsonSerializer.Deserialize<List<object>>(notes["inventory"]?.ToString() ?? "[]") ?? new List<object>();
            invList.RemoveAll(item =>
            {
                var obj = item as Dictionary<string, object>;
                return obj != null && obj.ContainsKey("Name") && obj["Name"].ToString() == itemName;
            });
            notes["inventory"] = invList;
            participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Removed {itemName}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> EquipItemAsync(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Equipped {itemName}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> UnequipItemAsync(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Unequipped {itemName}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
