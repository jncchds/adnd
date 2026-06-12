using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Inventory/Equipment ====================

    public async Task<CombatLogResponse> CombatAddItem(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AddItemToParticipantAsync(combatId, participantId, itemName, itemType, quantity, itemStats);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemAdded", new
        {
            participantId,
            itemName,
            itemType,
            quantity
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatRemoveItem(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RemoveItemFromParticipantAsync(combatId, participantId, itemName);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemRemoved", new
        {
            participantId,
            itemName
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatEquipItem(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.EquipItemAsync(combatId, participantId, itemName);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemEquipped", new
        {
            participantId,
            itemName
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatUnequipItem(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.UnequipItemAsync(combatId, participantId, itemName);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemUnequipped", new
        {
            participantId,
            itemName
        });
        return BuildCombatLog(result);
    }

}
