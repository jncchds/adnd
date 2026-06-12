using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>
/// Immutable registry of built-in RPG system definitions.
/// Sealed singleton — no interface, no runtime registration.
/// Custom systems are stored per-game in Game.CustomSystemJson and
/// resolved at the call site, not in this registry.
/// </summary>
public sealed class SystemRegistry
{
    private static readonly Dictionary<string, SystemDefinition> _builtins = new();

    static SystemRegistry()
    {
        RegisterBuiltinSystems();
    }

    /// <summary>
    /// Get a built-in system definition by ID.
    /// </summary>
    public static SystemDefinition? GetBuiltIn(string systemId)
    {
        return _builtins.TryGetValue(systemId, out var system) ? system : null;
    }

    /// <summary>
    /// Get all available built-in systems.
    /// </summary>
    public static IEnumerable<SystemDefinition> GetAllBuiltIn()
    {
        return _builtins.Values.ToList();
    }

    /// <summary>
    /// Deserialize a custom system definition from JSON.
    /// </summary>
    public static SystemDefinition? DeserializeCustom(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SystemDefinition>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void RegisterBuiltinSystems()
    {
        // D&D 5e
        _builtins["dnd5e"] = new SystemDefinition
        {
            Id = "dnd5e",
            Name = "Dungeons & Dragons 5th Edition",
            Version = "5.4",
            Description = "The most popular tabletop RPG. Roll d20 + modifier vs DC for skill checks, attacks, and saves.",
            DefaultCharacterJson = JsonSerializer.Serialize(new
            {
                name = "",
                @class = "Fighter",
                level = 1,
                currentHP = 10,
                maxHP = 10,
                attributes = new
                {
                    strength = 10,
                    dexterity = 10,
                    constitution = 10,
                    intelligence = 10,
                    wisdom = 10,
                    charisma = 10
                },
                skills = new Dictionary<string, int>
                {
                    { "Acrobatics", 0 }, { "Animal Handling", 0 }, { "Arcana", 0 },
                    { "Athletics", 0 }, { "Deception", 0 }, { "History", 0 },
                    { "Insight", 0 }, { "Intimidation", 0 }, { "Investigation", 0 },
                    { "Medicine", 0 }, { "Nature", 0 }, { "Perception", 0 },
                    { "Performance", 0 }, { "Persuasion", 0 }, { "Religion", 0 },
                    { "Sleight of Hand", 0 }, { "Stealth", 0 }, { "Survival", 0 }
                },
                proficiencyBonus = 2,
                inventory = Array.Empty<object>(),
                spells = Array.Empty<object>(),
                conditions = Array.Empty<object>(),
                customFields = new Dictionary<string, object>()
            }),
            AttributeNames = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" },
            AttributeRanges = new[]
            {
                ("strength", (1, 30)), ("dexterity", (1, 30)), ("constitution", (1, 30)),
                ("intelligence", (1, 30)), ("wisdom", (1, 30)), ("charisma", (1, 30))
            },
            RequiredCharacterFields = new[] { "name", "class", "level", "attributes" },
            SupportedDiceFormulas = new[] { "1d20", "2d6", "4d6kh3", "1d12" },
            SkillNames = new[]
            {
                "Acrobatics", "Animal Handling", "Arcana", "Athletics", "Deception",
                "History", "Insight", "Intimidation", "Investigation", "Medicine",
                "Nature", "Perception", "Performance", "Persuasion", "Religion",
                "Sleight of Hand", "Stealth", "Survival"
            }
        };

        // Pathfinder 2e
        _builtins["pf2e"] = new SystemDefinition
        {
            Id = "pf2e",
            Name = "Pathfinder 2nd Edition",
            Version = "2.3",
            Description = "A complex, tactical RPG with ancestry, class, and skill feats. Uses d20 + modifier vs DC.",
            DefaultCharacterJson = JsonSerializer.Serialize(new
            {
                name = "",
                @class = "Fighter",
                ancestry = "Human",
                level = 1,
                currentHP = 12,
                maxHP = 12,
                attributes = new
                {
                    strength = 10,
                    dexterity = 10,
                    constitution = 10,
                    intelligence = 10,
                    wisdom = 10,
                    charisma = 10
                },
                skills = new Dictionary<string, int>(),
                proficiency = new { untrained = 0, trained = 2, expert = 4, master = 6, legendary = 8 },
                inventory = Array.Empty<object>(),
                spells = Array.Empty<object>(),
                conditions = Array.Empty<object>(),
                customFields = new Dictionary<string, object>()
            }),
            AttributeNames = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" },
            AttributeRanges = new[]
            {
                ("strength", (1, 30)), ("dexterity", (1, 30)), ("constitution", (1, 30)),
                ("intelligence", (1, 30)), ("wisdom", (1, 30)), ("charisma", (1, 30))
            },
            RequiredCharacterFields = new[] { "name", "class", "level", "attributes" },
            SupportedDiceFormulas = new[] { "1d20", "2d6", "4d6kh3", "1d12" },
            SkillNames = new[] { "Acrobatics", "Arcana", "Athletics", "Crafting", "Deception", "Diplomacy",
                "Disguise", "Engage Lore", "Intimidation", "Investigation", "Life Science", "Medicine",
                "Occultism", "Performance", "Persuasion", "Religion", "Society", "Stealth", "Survival",
                "Thievery" }
        };

        // Call of Cthulhu 7e
        _builtins["coc7e"] = new SystemDefinition
        {
            Id = "coc7e",
            Name = "Call of Cthulhu 7th Edition",
            Version = "7.1",
            Description = "Horror investigation RPG. Roll d100 under skill for success. Criticals at 1/5 of skill.",
            DefaultCharacterJson = JsonSerializer.Serialize(new
            {
                name = "",
                occupation = "Investigator",
                currentSAN = 60,
                maxSAN = 60,
                currentHP = 12,
                maxHP = 12,
                attributes = new
                {
                    strength = 50,
                    constitution = 50,
                    size = 50,
                    intelligence = 50,
                    power = 50,
                    dexterity = 50,
                    apperance = 50
                },
                skills = new Dictionary<string, int>(),
                inventory = Array.Empty<object>(),
                conditions = Array.Empty<object>(),
                customFields = new Dictionary<string, object>()
            }),
            AttributeNames = new[] { "strength", "constitution", "size", "intelligence", "power", "dexterity", "appearance" },
            AttributeRanges = new[]
            {
                ("strength", (1, 100)), ("constitution", (1, 100)), ("size", (1, 100)),
                ("intelligence", (1, 100)), ("power", (1, 100)), ("dexterity", (1, 100)),
                ("appearance", (1, 100))
            },
            RequiredCharacterFields = new[] { "name", "occupation", "attributes" },
            SupportedDiceFormulas = new[] { "d100", "2d6", "1d10", "1d20" },
            SkillNames = new[]
            {
                "Animal Care", "Animal Riding", "Antiquities", "Archaeology", "Art",
                "Bureaucracy", "Chemistry", "Computer", "Credit Rating", "Dodge",
                "First Aid", "Forensics", "History", "Law", "Library Use",
                "Locksmith", "Mathematics", "Medicine", "Music", "Mythos",
                "Natural World", "Occult", "Persuade", "Psychology", "Research",
                "Science", "Shadow", "Speak Language", "Stealth", "Survival",
                "Throw", "Track", "X-Cult"
            }
        };
    }
}

/// <summary>
/// Defines an RPG system's rules, attributes, skills, and defaults.
/// Immutable data model — not a DI service.
/// </summary>
public class SystemDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? DefaultCharacterJson { get; set; }
    public string[]? AttributeNames { get; set; }
    public (string name, (int min, int max) range)[]? AttributeRanges { get; set; }
    public string[]? RequiredCharacterFields { get; set; }
    public string[]? SupportedDiceFormulas { get; set; }
    public string[]? SkillNames { get; set; }
    public Dictionary<string, object>? CustomRules { get; set; }
}
