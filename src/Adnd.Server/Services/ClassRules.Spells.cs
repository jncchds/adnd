using System.Text.Json;

namespace Adnd.Server.Services;

public static partial class ClassRules
{
    /// <summary>The ability a class casts with, or null for a class that casts nothing.</summary>
    public static string? SpellcastingAbility(string? className) => (className ?? "").Trim().ToLowerInvariant() switch
    {
        "wizard" => "INT",
        "cleric" or "druid" or "ranger" => "WIS",
        "bard" or "sorcerer" or "warlock" or "paladin" => "CHA",
        _ => null,
    };

    public static int SpellAttackBonus(string? className, int level, JsonElement attributes)
        => SpellcastingAbility(className) is { } ability ? ProficiencyBonus(level) + Modifier(attributes, ability) : 0;

    public static int SpellSaveDc(string? className, int level, JsonElement attributes)
        => SpellcastingAbility(className) is null ? 0 : 8 + SpellAttackBonus(className, level, attributes);

    /// <summary>
    /// The spells a new character of the class knows, keyed the way the sheet shows them. As
    /// with weapons and armour, how a spell resolves is part of its name — a caster used to
    /// start with slots and no spells, so the GM invented the list and skipped the saving
    /// throws: Burning Hands simply dealt its full damage to everything.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> StartingSpells(string? className) => (className ?? "").Trim().ToLowerInvariant() switch
    {
        "wizard" => Spells(
            ["Fire Bolt (spell attack, 1d10 fire)", "Ray of Frost (spell attack, 1d8 cold)", "Mage Hand", "Light"],
            ["Magic Missile (no roll, three darts of 1d4+1 force)", "Burning Hands (DEX save, 3d6 fire, half on a save, everyone in a zone)",
             "Sleep (no roll, puts 5d8 hit points of creatures to sleep)", "Shield (reaction, +5 AC until your next turn)", "Detect Magic"]),
        "sorcerer" => Spells(
            ["Fire Bolt (spell attack, 1d10 fire)", "Shocking Grasp (spell attack in melee, 1d8 lightning)", "Light", "Prestidigitation"],
            ["Magic Missile (no roll, three darts of 1d4+1 force)", "Burning Hands (DEX save, 3d6 fire, half on a save, everyone in a zone)"]),
        "warlock" => Spells(
            ["Eldritch Blast (spell attack, 1d10 force)", "Chill Touch (spell attack, 1d8 necrotic)"],
            ["Hellish Rebuke (reaction to being hit, DEX save, 2d10 fire, half on a save)", "Charm Person (WIS save or Charmed)"]),
        "cleric" => Spells(
            ["Sacred Flame (DEX save, 1d8 radiant)", "Guidance (+1d4 to one ability check)", "Light"],
            ["Cure Wounds (touch, heals 1d8 + WIS modifier)", "Healing Word (bonus action, heals 1d4 + WIS modifier)",
             "Guiding Bolt (spell attack, 4d6 radiant)", "Bless (+1d4 to attacks and saves for up to three allies)"]),
        "druid" => Spells(
            ["Produce Flame (spell attack, 1d8 fire)", "Guidance (+1d4 to one ability check)"],
            ["Cure Wounds (touch, heals 1d8 + WIS modifier)", "Healing Word (bonus action, heals 1d4 + WIS modifier)",
             "Thunderwave (CON save, 2d8 thunder, half on a save, everyone in your zone)", "Entangle (STR save or Restrained, everyone in a zone)"]),
        "bard" => Spells(
            ["Vicious Mockery (WIS save, 1d4 psychic)", "Minor Illusion"],
            ["Healing Word (bonus action, heals 1d4 + CHA modifier)", "Cure Wounds (touch, heals 1d8 + CHA modifier)",
             "Dissonant Whispers (WIS save, 3d6 psychic, half on a save)", "Faerie Fire (DEX save or outlined: attacks against them have advantage)"]),
        _ => new Dictionary<string, string[]>(),
    };

    private static Dictionary<string, string[]> Spells(string[] cantrips, string[] first)
        => new() { ["Cantrips"] = cantrips, ["Level 1"] = first };

    /// <summary>The sheet's spells as "Cantrips: a, b; Level 1: c" — empty when it lists none.</summary>
    public static string DescribeSpells(JsonElement stored)
    {
        if (stored.ValueKind != JsonValueKind.Object) return "";
        var groups = new List<string>();
        foreach (var group in stored.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Array) continue;
            var names = group.Value.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
            if (names.Count > 0) groups.Add($"{group.Name}: {string.Join(", ", names)}");
        }
        return string.Join("; ", groups);
    }
}
