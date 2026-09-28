using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public record LevelUp(int Level, int HitPointsGained, IReadOnlyList<string> NewFeatures);

/// <summary>
/// Every change to a player character's sheet outside a fight — hit points, conditions,
/// inventory, spell slots, rests, experience. The GM's tools are the only callers: the LLM runs
/// the game, and neither the players nor the game's creator edit these fields by hand.
///
/// Each change is pushed as <c>CharacterUpdated</c>, which open sheets and the chat's party
/// roster listen for. Characters are addressed by name, as NPCs and combatants are, because
/// the model is never given ids.
/// </summary>
public interface ICharacterStateService
{
    Task<Character?> FindAsync(Guid gameId, string name, CancellationToken ct = default);

    /// <summary>"Aria, Thorin" — for an error the model can correct itself from.</summary>
    Task<string> PartyNamesAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>The whole party, or the named members of it; unknown names are returned separately.</summary>
    Task<(List<Character> Found, List<string> Unknown)> ResolveAsync(Guid gameId, IReadOnlyList<string>? names, CancellationToken ct = default);

    Task<InventoryItem> GiveItemAsync(Character character, string item, int quantity, CancellationToken ct = default);

    /// <summary>Null when the character has no such item; otherwise what is left of the stack (0 when used up).</summary>
    Task<int?> RemoveItemAsync(Character character, string item, int quantity, CancellationToken ct = default);

    /// <summary>Spends one slot of the level, or of the lowest level above it that has one left.
    /// Returns the level actually spent, or null when nothing at or above it remains.</summary>
    Task<int?> SpendSpellSlotAsync(Character character, int level, CancellationToken ct = default);

    /// <summary>Out-of-combat hit point change; negative is damage.</summary>
    Task ChangeHpAsync(Character character, int amount, CancellationToken ct = default);

    Task SetConditionAsync(Character character, string condition, bool applied, CancellationToken ct = default);

    /// <summary>Returns what the rest restored, for the table and for the model.</summary>
    Task<string> RestAsync(Character character, RestPeriod period, CancellationToken ct = default);

    /// <summary>Adds XP and applies every level it crosses.</summary>
    Task<IReadOnlyList<LevelUp>> AwardExperienceAsync(Character character, int amount, CancellationToken ct = default);

    /// <summary>A line in the log, as the table would hear it.</summary>
    Task AnnounceAsync(Guid gameId, Guid sessionId, string content, CancellationToken ct = default);
}

public class CharacterStateService(
    AppDbContext db,
    IRerollService rerolls,
    IFeatureCatalogue features,
    IHubContext<GameHub> hub) : ICharacterStateService
{
    public async Task<Character?> FindAsync(Guid gameId, string name, CancellationToken ct = default)
    {
        var lowered = name.Trim().ToLowerInvariant();
        return await db.Characters.FirstOrDefaultAsync(
            c => c.Player.GameId == gameId && c.Name.ToLower() == lowered, ct);
    }

    public async Task<string> PartyNamesAsync(Guid gameId, CancellationToken ct = default)
    {
        var names = await db.Characters.Where(c => c.Player.GameId == gameId).Select(c => c.Name).ToListAsync(ct);
        return names.Count == 0 ? "(no player characters)" : string.Join(", ", names);
    }

    public async Task<(List<Character> Found, List<string> Unknown)> ResolveAsync(Guid gameId, IReadOnlyList<string>? names, CancellationToken ct = default)
    {
        var party = await db.Characters.Where(c => c.Player.GameId == gameId).ToListAsync(ct);
        if (names is null || names.Count == 0) return (party, []);

        var found = new List<Character>();
        var unknown = new List<string>();
        foreach (var name in names)
        {
            var match = party.FirstOrDefault(c => c.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null) unknown.Add(name);
            else if (!found.Contains(match)) found.Add(match);
        }
        return (found, unknown);
    }

    public async Task<InventoryItem> GiveItemAsync(Character character, string item, int quantity, CancellationToken ct = default)
    {
        var items = ClassRules.ReadInventory(character.Inventory);
        var index = items.FindIndex(i => i.Name.Equals(item.Trim(), StringComparison.OrdinalIgnoreCase));
        var stack = index >= 0
            ? items[index] with { Quantity = items[index].Quantity + quantity }
            : new InventoryItem(item.Trim(), quantity);
        if (index >= 0) items[index] = stack; else items.Add(stack);

        await SaveInventoryAsync(character, items, ct);
        return stack;
    }

    public async Task<int?> RemoveItemAsync(Character character, string item, int quantity, CancellationToken ct = default)
    {
        var items = ClassRules.ReadInventory(character.Inventory);
        var index = items.FindIndex(i => i.Name.Equals(item.Trim(), StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;

        var left = Math.Max(0, items[index].Quantity - quantity);
        if (left == 0) items.RemoveAt(index); else items[index] = items[index] with { Quantity = left };

        await SaveInventoryAsync(character, items, ct);
        return left;
    }

    public async Task<int?> SpendSpellSlotAsync(Character character, int level, CancellationToken ct = default)
    {
        // Refit first, so a sheet created before slots were tracked gets its table on first cast.
        var slots = ClassRules.RefitSlots(character.SpellSlots, character.Class, character.Level);

        // Upcasting is allowed; downcasting is not. The lowest available slot at or above the
        // spell's level is the one a player would spend.
        var spend = slots
            .Select(kv => (Level: int.Parse(kv.Key), Slot: kv.Value))
            .Where(x => x.Level >= Math.Max(level, 1) && x.Slot.Remaining > 0)
            .OrderBy(x => x.Level)
            .Select(x => (int?)x.Level)
            .FirstOrDefault();
        if (spend is not { } spent) return null;

        var key = spent.ToString();
        slots[key] = slots[key] with { Remaining = slots[key].Remaining - 1 };
        character.SpellSlots = JsonSerializer.SerializeToElement(slots, ClassRules.Json);
        await SaveAndNotifyAsync(character, ct);
        return spent;
    }

    public async Task ChangeHpAsync(Character character, int amount, CancellationToken ct = default)
    {
        character.CurrentHP = Math.Clamp(character.CurrentHP + amount, 0, character.MaxHP);
        await SaveAndNotifyAsync(character, ct);
    }

    public async Task SetConditionAsync(Character character, string condition, bool applied, CancellationToken ct = default)
    {
        character.Conditions = WithCondition(character.Conditions, condition, applied);
        await SaveAndNotifyAsync(character, ct);
    }

    /// <summary>Shared with the combat tracker, which mirrors a PC's conditions onto the sheet.</summary>
    internal static JsonElement WithCondition(JsonElement stored, string condition, bool applied)
    {
        var conditions = CombatTracker.ReadConditions(stored)
            .Where(c => !c.Equals(condition.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (applied) conditions.Add(condition.Trim());
        return JsonSerializer.SerializeToElement(conditions);
    }

    public async Task<string> RestAsync(Character character, RestPeriod period, CancellationToken ct = default)
    {
        var restored = new List<string>();

        if (period == RestPeriod.LongRest)
        {
            if (character.CurrentHP < character.MaxHP)
            {
                character.CurrentHP = character.MaxHP;
                restored.Add("hit points");
            }

            var slots = ClassRules.RefitSlots(character.SpellSlots, character.Class, character.Level);
            if (slots.Values.Any(s => s.Remaining < s.Total)) restored.Add("spell slots");
            character.SpellSlots = JsonSerializer.SerializeToElement(
                slots.ToDictionary(kv => kv.Key, kv => kv.Value with { Remaining = kv.Value.Total }), ClassRules.Json);
        }

        await db.SaveChangesAsync(ct);

        // Abilities are refilled by the reroll service, which knows each one's recharge rule.
        var abilities = await rerolls.RestoreOnRestAsync(character.Id, period, ct);
        if (abilities > 0) restored.Add(abilities == 1 ? "1 ability use" : $"{abilities} ability uses");

        await NotifyAsync(character, ct);
        return restored.Count == 0 ? "nothing needed restoring" : string.Join(", ", restored);
    }

    public async Task<IReadOnlyList<LevelUp>> AwardExperienceAsync(Character character, int amount, CancellationToken ct = default)
    {
        character.Experience = Math.Max(0, character.Experience + amount);

        var levelUps = new List<LevelUp>();
        var target = ClassRules.LevelForXp(character.Experience);
        while (character.Level < target)
        {
            character.Level++;
            character.ProficiencyBonus = ClassRules.ProficiencyBonus(character.Level);

            // The die's fixed average rather than a roll: a level-up is not a moment anyone
            // should be asked to gamble on, and it keeps the GM out of the arithmetic.
            var gained = ClassRules.HitPointsPerLevel(character.Class, character.Attributes);
            character.MaxHP += gained;
            character.CurrentHP += gained;

            character.SpellSlots = JsonSerializer.SerializeToElement(
                ClassRules.RefitSlots(character.SpellSlots, character.Class, character.Level), ClassRules.Json);

            // Class abilities arrive at their level, as racial ones did at creation.
            var owned = rerolls.Read(character.Features).ToList();
            var granted = features.GrantedTo(character.Race, character.Class, character.Level)
                .Where(d => !owned.Any(o => o.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (granted.Count > 0)
            {
                owned.AddRange(granted.Select(d => new CharacterFeature(d.Id, d.Name, d.MaxUses)));
                character.Features = rerolls.Write(owned);
            }

            levelUps.Add(new LevelUp(character.Level, gained, granted.Select(d => d.Name).ToList()));
        }

        await SaveAndNotifyAsync(character, ct);
        return levelUps;
    }

    public async Task AnnounceAsync(Guid gameId, Guid sessionId, string content, CancellationToken ct = default)
    {
        var msg = new Message
        {
            SessionId = sessionId,
            Content = content,
            Type = "System",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);
        await hub.Clients.Group(gameId.ToString()).SendAsync("NewMessage",
            new MessageDto(msg.Id, sessionId, null, content, msg.Type, false, msg.CreatedAt, null), ct);
    }

    private async Task SaveInventoryAsync(Character character, List<InventoryItem> items, CancellationToken ct)
    {
        character.Inventory = JsonSerializer.SerializeToElement(items, ClassRules.Json);
        await SaveAndNotifyAsync(character, ct);
    }

    private async Task SaveAndNotifyAsync(Character character, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await NotifyAsync(character, ct);
    }

    private async Task NotifyAsync(Character character, CancellationToken ct)
    {
        var gameId = await db.Players.IgnoreQueryFilters()
            .Where(p => p.Id == character.PlayerId).Select(p => p.GameId).FirstOrDefaultAsync(ct);
        await hub.Clients.Group(gameId.ToString()).SendAsync("CharacterUpdated", new { characterId = character.Id }, ct);
    }
}
