using System.Text.Json;
using Adnd.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

public sealed class CombatInventoryService(AppDbContext db) : ICombatInventoryService
{
    /// <summary>
    /// Consumes one quantity of an item from the character's inventory.
    /// Removes the item if quantity drops to zero.
    /// Expected jsonb format: {"sword": {"name": "Sword", "quantity": 1}, ...}
    /// </summary>
    public async Task UseItemAsync(Guid participantId, string itemName, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants.FindAsync([participantId], ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        if (!participant.CharacterId.HasValue) return;

        var character = await db.Characters.FindAsync([participant.CharacterId.Value], ct);
        if (character is null) return;

        if (character.Inventory.ValueKind == JsonValueKind.Undefined ||
            character.Inventory.ValueKind == JsonValueKind.Null)
            return;

        var inventory = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            character.Inventory.GetRawText()) ?? new();

        // Find item by key or by "name" property (case-insensitive)
        string? foundKey = null;
        foreach (var (key, value) in inventory)
        {
            if (key.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                (value.TryGetProperty("name", out var nameEl) &&
                 nameEl.GetString()?.Equals(itemName, StringComparison.OrdinalIgnoreCase) == true))
            {
                foundKey = key;
                break;
            }
        }

        if (foundKey is null) return;

        var item = inventory[foundKey];
        int qty = 1;
        if (item.TryGetProperty("quantity", out var qtyEl))
            qtyEl.TryGetInt32(out qty);

        if (qty <= 1)
        {
            inventory.Remove(foundKey);
        }
        else
        {
            // Rebuild item with decremented quantity
            var itemDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.GetRawText()) ?? new();
            itemDict["quantity"] = JsonSerializer.Deserialize<JsonElement>((qty - 1).ToString());
            inventory[foundKey] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(itemDict));
        }

        character.Inventory = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(inventory));
        await db.SaveChangesAsync(ct);
    }
}
