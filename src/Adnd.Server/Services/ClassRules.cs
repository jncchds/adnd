using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adnd.Server.Services;

/// <summary>One spell level's slots, as stored in <c>Character.SpellSlots</c> keyed by level ("1".."9").</summary>
public record SpellSlotLevel(int Total, int Remaining);

/// <summary>One stack in <c>Character.Inventory</c>.</summary>
public record InventoryItem(string Name, int Quantity);

/// <summary>
/// The 5e numbers a character's progression follows: hit dice, spell slots, proficiency and the
/// XP table. Class is free text on the sheet, so anything unrecognised falls back to a d8
/// non-caster rather than failing — the GM can still run that character, it just gets no slots.
/// </summary>
public static partial class ClassRules
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public const int MaxLevel = 20;

    /// <summary>Total XP needed to reach each level; index 0 is level 1.</summary>
    private static readonly int[] XpThresholds =
    [
        0, 300, 900, 2700, 6500, 14000, 23000, 34000, 48000, 64000,
        85000, 100000, 120000, 140000, 165000, 195000, 225000, 265000, 305000, 355000
    ];

    private enum Caster { None, Full, Half, Pact }

    private static readonly Dictionary<string, (int HitDie, Caster Caster)> Classes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Barbarian"] = (12, Caster.None),
        ["Fighter"] = (10, Caster.None),
        ["Paladin"] = (10, Caster.Half),
        ["Ranger"] = (10, Caster.Half),
        ["Bard"] = (8, Caster.Full),
        ["Cleric"] = (8, Caster.Full),
        ["Druid"] = (8, Caster.Full),
        ["Monk"] = (8, Caster.None),
        ["Rogue"] = (8, Caster.None),
        ["Warlock"] = (8, Caster.Pact),
        ["Sorcerer"] = (6, Caster.Full),
        ["Wizard"] = (6, Caster.Full),
    };

    /// <summary>Full-caster slots per spell level, by caster level 1..20.</summary>
    private static readonly int[][] FullCasterSlots =
    [
        [2], [3], [4, 2], [4, 3], [4, 3, 2], [4, 3, 3], [4, 3, 3, 1], [4, 3, 3, 2], [4, 3, 3, 3, 1], [4, 3, 3, 3, 2],
        [4, 3, 3, 3, 2, 1], [4, 3, 3, 3, 2, 1], [4, 3, 3, 3, 2, 1, 1], [4, 3, 3, 3, 2, 1, 1], [4, 3, 3, 3, 2, 1, 1, 1],
        [4, 3, 3, 3, 2, 1, 1, 1], [4, 3, 3, 3, 2, 1, 1, 1, 1], [4, 3, 3, 3, 3, 1, 1, 1, 1], [4, 3, 3, 3, 3, 2, 1, 1, 1],
        [4, 3, 3, 3, 3, 2, 2, 1, 1],
    ];

    public static int HitDie(string? className) => Lookup(className).HitDie;

    /// <summary>
    /// What a new character of the class is armed with. The damage is part of the item's
    /// name on purpose: a sheet used to start with rations and a tinderbox and no weapon, so
    /// every attack began with the GM looking the sheet up and then inventing a sword. Named
    /// like this, the narrator's prompt can list each character's weapons and their dice.
    /// </summary>
    public static IReadOnlyList<string> StartingWeapons(string? className) => (className ?? "").Trim().ToLowerInvariant() switch
    {
        "barbarian" => ["Greataxe (1d12 slashing)", "Javelin (1d6 piercing, thrown)"],
        "fighter" => ["Longsword (1d8 slashing)", "Light crossbow (1d8 piercing, ranged)"],
        "paladin" => ["Longsword (1d8 slashing)", "Javelin (1d6 piercing, thrown)"],
        "ranger" => ["Longbow (1d8 piercing, ranged)", "Shortsword (1d6 piercing, finesse)"],
        "bard" => ["Rapier (1d8 piercing, finesse)", "Dagger (1d4 piercing, finesse)"],
        "cleric" => ["Mace (1d6 bludgeoning)", "Light crossbow (1d8 piercing, ranged)"],
        "druid" => ["Scimitar (1d6 slashing, finesse)", "Sling (1d4 bludgeoning, ranged)"],
        "monk" => ["Shortsword (1d6 piercing, finesse)", "Dart (1d4 piercing, thrown)"],
        "rogue" => ["Rapier (1d8 piercing, finesse)", "Shortbow (1d6 piercing, ranged)"],
        "warlock" or "sorcerer" => ["Dagger (1d4 piercing, finesse)", "Light crossbow (1d8 piercing, ranged)"],
        "wizard" => ["Quarterstaff (1d6 bludgeoning)", "Dagger (1d4 piercing, finesse)"],
        _ => ["Dagger (1d4 piercing, finesse)"],
    };

    /// <summary>The items on a sheet that are weapons — the ones that name their damage dice.</summary>
    public static IEnumerable<string> Weapons(JsonElement inventory)
        => ReadInventory(inventory).Select(i => i.Name).Where(n => WeaponDice().IsMatch(n));

    [System.Text.RegularExpressions.GeneratedRegex(@"\(\d*d\d+")]
    private static partial System.Text.RegularExpressions.Regex WeaponDice();

    /// <summary>
    /// What a new character of the class wears. Like a weapon's dice, the armour class is part
    /// of the item's name — <c>Chain mail (AC 16)</c>, <c>Leather armour (AC 11 + DEX)</c>,
    /// <c>Scale mail (AC 14 + DEX, max 2)</c>, <c>Shield (+2 AC)</c> — so armour the GM hands
    /// out with <c>giveItem</c> counts the moment it is on the sheet. Every character used to
    /// be AC 10 + DEX whatever they wore, and a goblin hit a fighter on a 7.
    /// </summary>
    public static IReadOnlyList<string> StartingArmour(string? className) => (className ?? "").Trim().ToLowerInvariant() switch
    {
        "fighter" or "paladin" => ["Chain mail (AC 16)", "Shield (+2 AC)"],
        "cleric" => ["Scale mail (AC 14 + DEX, max 2)", "Shield (+2 AC)"],
        "ranger" => ["Scale mail (AC 14 + DEX, max 2)"],
        "druid" => ["Leather armour (AC 11 + DEX)", "Shield (+2 AC)"],
        "bard" or "rogue" or "warlock" => ["Leather armour (AC 11 + DEX)"],
        // Barbarians and monks fight unarmoured (see ArmourClass); sorcerers and wizards own none.
        _ => [],
    };

    /// <summary>The items on a sheet that are armour or a shield — the ones that name an AC.</summary>
    public static IEnumerable<string> Armour(JsonElement inventory)
        => ReadInventory(inventory).Select(i => i.Name).Where(n => BodyArmour().IsMatch(n) || ShieldBonus().IsMatch(n));

    /// <summary>
    /// Armour class as the sheet has it: the best body armour carried (or none — 10 + DEX, plus
    /// CON for a barbarian and WIS for a monk without a shield) and the best shield.
    /// </summary>
    public static int ArmourClass(string? className, JsonElement inventory, JsonElement attributes)
    {
        var dex = Modifier(attributes, "DEX");
        var names = ReadInventory(inventory).Select(i => i.Name).ToList();
        var shield = names.Select(n => ShieldBonus().Match(n)).Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();

        var unarmoured = 10 + dex + (className ?? "").Trim().ToLowerInvariant() switch
        {
            "barbarian" => Math.Max(0, Modifier(attributes, "CON")),
            "monk" when shield == 0 => Math.Max(0, Modifier(attributes, "WIS")),
            _ => 0,
        };
        var body = names.Select(n => BodyArmour().Match(n)).Where(m => m.Success).Select(m =>
        {
            var ac = int.Parse(m.Groups[1].Value);
            if (!m.Groups[2].Success) return ac;
            return ac + (m.Groups[3].Success ? Math.Min(dex, int.Parse(m.Groups[3].Value)) : dex);
        }).DefaultIfEmpty(unarmoured).Max();

        return Math.Max(body, unarmoured) + shield;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\(\s*AC\s*(\d{1,2})(\s*\+\s*DEX)?(?:\s*,?\s*max\s*\+?(\d))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex BodyArmour();

    [System.Text.RegularExpressions.GeneratedRegex(@"\(\s*\+(\d)\s*AC", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ShieldBonus();

    public static int ProficiencyBonus(int level) => 2 + (Math.Clamp(level, 1, MaxLevel) - 1) / 4;

    public static int LevelForXp(int xp)
    {
        var level = 1;
        while (level < MaxLevel && xp >= XpThresholds[level]) level++;
        return level;
    }

    /// <summary>XP at which the next level arrives, or null at the cap.</summary>
    public static int? XpForNextLevel(int level) => level >= MaxLevel ? null : XpThresholds[level];

    /// <summary>Hit points gained on reaching a new level: the die's fixed average plus CON, never below 1.</summary>
    public static int HitPointsPerLevel(string? className, JsonElement attributes)
        => Math.Max(1, HitDie(className) / 2 + 1 + Modifier(attributes, "CON"));

    /// <summary>Maximum hit points at first level: the whole die plus CON.</summary>
    public static int FirstLevelHitPoints(string? className, JsonElement attributes)
        => Math.Max(1, HitDie(className) + Modifier(attributes, "CON"));

    /// <summary>Slots per spell level for a class at a level; empty for non-casters.</summary>
    public static IReadOnlyDictionary<int, int> SpellSlots(string? className, int level)
    {
        level = Math.Clamp(level, 1, MaxLevel);
        int[] row = Lookup(className).Caster switch
        {
            Caster.Full => FullCasterSlots[level - 1],
            // Paladins and rangers cast from 2nd level at half their level, rounded up.
            Caster.Half => level < 2 ? [] : FullCasterSlots[(level + 1) / 2 - 1],
            // Warlock pact magic: few slots, all of the highest level they can cast.
            Caster.Pact => PactSlots(level),
            _ => [],
        };
        return row.Select((count, i) => (Level: i + 1, count))
            .Where(x => x.count > 0)
            .ToDictionary(x => x.Level, x => x.count);
    }

    private static int[] PactSlots(int level)
    {
        var count = level switch { 1 => 1, <= 10 => 2, <= 16 => 3, _ => 4 };
        var slotLevel = Math.Min(5, (level + 1) / 2);
        var row = new int[slotLevel];
        row[slotLevel - 1] = count;
        return row;
    }

    /// <summary>
    /// Refits stored slots to a new level: totals move to the table, and remaining uses move by
    /// the same amount the total did — a level-up grants the new slots without refilling the
    /// ones already spent today.
    /// </summary>
    public static Dictionary<string, SpellSlotLevel> RefitSlots(JsonElement stored, string? className, int level)
    {
        var current = ReadSlots(stored);
        var result = new Dictionary<string, SpellSlotLevel>();
        foreach (var (slotLevel, total) in SpellSlots(className, level))
        {
            var key = slotLevel.ToString();
            var before = current.GetValueOrDefault(key);
            var remaining = before is null ? total : Math.Clamp(before.Remaining + (total - before.Total), 0, total);
            result[key] = new SpellSlotLevel(total, remaining);
        }
        return result;
    }

    public static Dictionary<string, SpellSlotLevel> ReadSlots(JsonElement stored)
    {
        if (stored.ValueKind != JsonValueKind.Object) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, SpellSlotLevel>>(stored.GetRawText(), Json) ?? [];
        }
        catch (JsonException)
        {
            // A sheet from before slots were structured; the next refit rebuilds it from the table.
            return [];
        }
    }

    /// <summary>
    /// Inventory as stacks. Older sheets store bare strings ("Rope"), which read as a stack of
    /// one; anything else unreadable is skipped rather than failing the whole sheet.
    /// </summary>
    public static List<InventoryItem> ReadInventory(JsonElement stored)
    {
        var items = new List<InventoryItem>();
        if (stored.ValueKind != JsonValueKind.Array) return items;
        foreach (var el in stored.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } bare)
                items.Add(new InventoryItem(bare, 1));
            else if (el.ValueKind == JsonValueKind.Object
                     && el.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                     && n.GetString() is { Length: > 0 } name)
                items.Add(new InventoryItem(name,
                    el.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number && q.TryGetInt32(out var qty) ? Math.Max(qty, 1) : 1));
        }
        return items;
    }

    public static int Modifier(JsonElement attributes, string ability)
        => attributes.ValueKind == JsonValueKind.Object
           && attributes.TryGetProperty(ability, out var value)
           && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var score)
            ? AbilityScoreHelper.GetModifier(score)
            : 0;

    private static (int HitDie, Caster Caster) Lookup(string? className)
        => className is not null && Classes.TryGetValue(className.Trim(), out var rules) ? rules : (8, Caster.None);
}
