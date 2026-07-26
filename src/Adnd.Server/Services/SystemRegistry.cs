namespace Adnd.Server.Services;

public record AttributeDef(string Id, string Name, string Abbreviation);
public record SkillDef(string Id, string Name, string Attribute);
public record SystemDefinition(
    string Id,
    string Name,
    string Version,
    string Description,
    string DiceType,
    IReadOnlyList<AttributeDef> Attributes,
    IReadOnlyList<SkillDef> Skills);

public interface ISystemRegistry
{
    IReadOnlyList<SystemDefinition> GetBuiltInSystems();
    SystemDefinition? GetById(string id);
}

public class SystemRegistry : ISystemRegistry
{
    private static readonly IReadOnlyList<SystemDefinition> _systems = BuildSystems();

    public IReadOnlyList<SystemDefinition> GetBuiltInSystems() => _systems;

    public SystemDefinition? GetById(string id) =>
        _systems.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<SystemDefinition> BuildSystems()
    {
        return
        [
            BuildDnd5e(),
            BuildPf2e(),
            BuildCoc7e()
        ];
    }

    private static SystemDefinition BuildDnd5e()
    {
        var attrs = new List<AttributeDef>
        {
            new("str", "Strength", "STR"),
            new("dex", "Dexterity", "DEX"),
            new("con", "Constitution", "CON"),
            new("int", "Intelligence", "INT"),
            new("wis", "Wisdom", "WIS"),
            new("cha", "Charisma", "CHA")
        };

        var skills = new List<SkillDef>
        {
            new("acrobatics", "Acrobatics", "dex"),
            new("animal_handling", "Animal Handling", "wis"),
            new("arcana", "Arcana", "int"),
            new("athletics", "Athletics", "str"),
            new("deception", "Deception", "cha"),
            new("history", "History", "int"),
            new("insight", "Insight", "wis"),
            new("intimidation", "Intimidation", "cha"),
            new("investigation", "Investigation", "int"),
            new("medicine", "Medicine", "wis"),
            new("nature", "Nature", "int"),
            new("perception", "Perception", "wis"),
            new("performance", "Performance", "cha"),
            new("persuasion", "Persuasion", "cha"),
            new("religion", "Religion", "int"),
            new("sleight_of_hand", "Sleight of Hand", "dex"),
            new("stealth", "Stealth", "dex"),
            new("survival", "Survival", "wis")
        };

        return new SystemDefinition(
            "dnd5e",
            "D&D 5th Edition",
            "5.1",
            "Dungeons & Dragons 5th Edition with six core attributes and twenty skills.",
            "d20",
            attrs,
            skills);
    }

    private static SystemDefinition BuildPf2e()
    {
        var attrs = new List<AttributeDef>
        {
            new("str", "Strength", "STR"),
            new("dex", "Dexterity", "DEX"),
            new("con", "Constitution", "CON"),
            new("int", "Intelligence", "INT"),
            new("wis", "Wisdom", "WIS"),
            new("cha", "Charisma", "CHA")
        };

        var skills = new List<SkillDef>
        {
            new("acrobatics", "Acrobatics", "dex"),
            new("arcana", "Arcana", "int"),
            new("athletics", "Athletics", "str"),
            new("crafting", "Crafting", "int"),
            new("deception", "Deception", "cha"),
            new("diplomacy", "Diplomacy", "cha"),
            new("intimidation", "Intimidation", "cha"),
            new("lore", "Lore", "int"),
            new("medicine", "Medicine", "wis"),
            new("nature", "Nature", "wis"),
            new("occultism", "Occultism", "int"),
            new("performance", "Performance", "cha"),
            new("religion", "Religion", "wis"),
            new("society", "Society", "int"),
            new("stealth", "Stealth", "dex"),
            new("survival", "Survival", "wis"),
            new("thievery", "Thievery", "dex")
        };

        return new SystemDefinition(
            "pf2e",
            "Pathfinder 2nd Edition",
            "2.0",
            "Pathfinder 2nd Edition with proficiency-based skill checks and six core attributes.",
            "d20",
            attrs,
            skills);
    }

    private static SystemDefinition BuildCoc7e()
    {
        var attrs = new List<AttributeDef>
        {
            new("str", "Strength", "STR"),
            new("con", "Constitution", "CON"),
            new("siz", "Size", "SIZ"),
            new("dex", "Dexterity", "DEX"),
            new("app", "Appearance", "APP"),
            new("int", "Intelligence", "INT"),
            new("pow", "Power", "POW"),
            new("edu", "Education", "EDU")
        };

        var skills = new List<SkillDef>
        {
            new("accounting", "Accounting", "edu"),
            new("anthropology", "Anthropology", "edu"),
            new("appraise", "Appraise", "int"),
            new("archaeology", "Archaeology", "edu"),
            new("charm", "Charm", "app"),
            new("climb", "Climb", "str"),
            new("credit_rating", "Credit Rating", "edu"),
            new("cthulhu_mythos", "Cthulhu Mythos", "int"),
            new("disguise", "Disguise", "app"),
            new("dodge", "Dodge", "dex"),
            new("drive_auto", "Drive Auto", "dex"),
            new("electrical_repair", "Electrical Repair", "edu"),
            new("fast_talk", "Fast Talk", "app"),
            new("fighting_brawl", "Fighting (Brawl)", "str"),
            new("first_aid", "First Aid", "edu"),
            new("history", "History", "edu"),
            new("intimidate", "Intimidate", "str"),
            new("jump", "Jump", "str"),
            new("library_use", "Library Use", "edu"),
            new("listen", "Listen", "int"),
            new("locksmith", "Locksmith", "dex"),
            new("mechanical_repair", "Mechanical Repair", "edu"),
            new("medicine", "Medicine", "edu"),
            new("natural_world", "Natural World", "int"),
            new("navigate", "Navigate", "int"),
            new("occult", "Occult", "int"),
            new("persuade", "Persuade", "app"),
            new("psychology", "Psychology", "int"),
            new("psychoanalysis", "Psychoanalysis", "edu"),
            new("ride", "Ride", "dex"),
            new("science", "Science", "edu"),
            new("sleight_of_hand", "Sleight of Hand", "dex"),
            new("spot_hidden", "Spot Hidden", "int"),
            new("stealth", "Stealth", "dex"),
            new("survival", "Survival", "con"),
            new("swim", "Swim", "str"),
            new("throw", "Throw", "dex"),
            new("track", "Track", "int")
        };

        return new SystemDefinition(
            "coc7e",
            "Call of Cthulhu 7th Edition",
            "7.0",
            "Call of Cthulhu 7th Edition — investigative horror RPG with d100 percentile skill checks and Sanity mechanics.",
            "d100",
            attrs,
            skills);
    }
}
