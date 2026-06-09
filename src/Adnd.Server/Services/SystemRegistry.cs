using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>
/// Registry of supported RPG systems with their rules and defaults.
/// Pluggable architecture allows custom systems to be added at runtime.
/// </summary>
public interface ISystemRegistry
{
    /// <summary>
    /// Get a system definition by ID.
    /// </summary>
    SystemDefinition? GetSystem(string systemId);

    /// <summary>
    /// Get all available systems.
    /// </summary>
    IEnumerable<SystemDefinition> GetAllSystems();

    /// <summary>
    /// Register a custom system at runtime.
    /// </summary>
    void RegisterSystem(SystemDefinition definition);

    /// <summary>
    /// Create a new character sheet for the given system with default values.
    /// </summary>
    JsonDocument CreateDefaultCharacter(string systemId);

    /// <summary>
    /// Validate a character sheet against the system's rules.
    /// </summary>
    (bool valid, string[] errors) ValidateCharacter(string systemId, JsonElement character);
}

public class SystemRegistry : ISystemRegistry
{
    private readonly Dictionary<string, SystemDefinition> _systems = new();
    private readonly ILogger<SystemRegistry> _logger;
    private readonly ISystemRulesFactory _rulesFactory;

    public SystemRegistry(ILogger<SystemRegistry> logger, ISystemRulesFactory rulesFactory)
    {
        _logger = logger;
        _rulesFactory = rulesFactory;
        RegisterBuiltinSystems();
    }

    public SystemDefinition? GetSystem(string systemId)
    {
        return _systems.TryGetValue(systemId, out var system) ? system : null;
    }

    public IEnumerable<SystemDefinition> GetAllSystems()
    {
        return _systems.Values.ToList();
    }

    public void RegisterSystem(SystemDefinition definition)
    {
        if (_systems.ContainsKey(definition.Id))
        {
            _logger.LogWarning("Overwriting system definition for '{SystemId}'", definition.Id);
        }
        _systems[definition.Id] = definition;
        _logger.LogInformation("Registered system: {SystemId} v{Version}", definition.Id, definition.Version);
    }

    public JsonDocument CreateDefaultCharacter(string systemId)
    {
        var system = GetSystem(systemId);
        if (system == null)
            throw new InvalidOperationException($"Unknown system: {systemId}");

        var defaults = system.DefaultCharacterJson;
        if (!string.IsNullOrEmpty(defaults))
        {
            return JsonDocument.Parse(defaults);
        }

        // Generic fallback
        var fallback = new
        {
            attributes = new { str = 10, dex = 10, con = 10, @int = 10, wis = 10, cha = 10 },
            skills = new Dictionary<string, int>(),
            inventory = Array.Empty<object>(),
            spells = Array.Empty<object>(),
            conditions = Array.Empty<object>(),
            customFields = new Dictionary<string, object>()
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(fallback));
    }

    public (bool valid, string[] errors) ValidateCharacter(string systemId, JsonElement character)
    {
        var system = GetSystem(systemId);
        if (system == null)
            return (false, [$"Unknown system: {systemId}"]);

        var errors = new List<string>();

        // Validate required fields
        if (system.RequiredCharacterFields != null)
        {
            foreach (var field in system.RequiredCharacterFields)
            {
                if (!character.TryGetProperty(field, out _))
                    errors.Add($"Missing required field: {field}");
            }
        }

        // Validate attribute ranges
        if (system.AttributeRanges != null && character.TryGetProperty("attributes", out var attrs))
        {
            foreach (var (attrName, (min, max)) in system.AttributeRanges)
            {
                if (attrs.TryGetProperty(attrName, out var attrVal))
                {
                    if (attrVal.ValueKind == JsonValueKind.Number)
                    {
                        var val = attrVal.GetInt32();
                        if (val < min || val > max)
                            errors.Add($"{attrName} value {val} out of range [{min}, {max}]");
                    }
                }
            }
        }

        return (errors.Count == 0, errors.ToArray());
    }

    private void RegisterBuiltinSystems()
    {
        // D&D 5e
        RegisterSystem(new SystemDefinition
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
        });

        // Pathfinder 2e
        RegisterSystem(new SystemDefinition
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
        });

        // Call of Cthulhu 7e
        RegisterSystem(new SystemDefinition
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
        });
    }
}

/// <summary>
/// Defines an RPG system's rules, attributes, skills, and defaults.
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
