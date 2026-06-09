using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Strategy interface for character creation flows.
/// Each RPG system has its own character creation strategy that handles
/// default values, validation, and system-specific initialization.
/// </summary>
public interface ICharacterCreationStrategy
{
    /// <summary>
    /// Gets the system ID this strategy handles.
    /// </summary>
    string SystemId { get; }

    /// <summary>
    /// Creates a default character template for this system.
    /// </summary>
    JsonDocument CreateDefaultTemplate();

    /// <summary>
    /// Validates a character against this system's rules.
    /// </summary>
    (bool valid, string[] errors) Validate(JsonElement character);

    /// <summary>
    /// Applies system-specific initialization after character creation.
    /// </summary>
    Task<Character> InitializeAsync(Character character, AppDbContext context);
}

/// <summary>
/// Factory for creating character creation strategies.
/// </summary>
public interface ICharacterCreationFactory
{
    ICharacterCreationStrategy GetStrategy(string systemId);
    bool HasStrategy(string systemId);
    void RegisterStrategy(ICharacterCreationStrategy strategy);
}

/// <summary>
/// Concrete strategy for D&amp;D 5e character creation.
/// </summary>
public class DnD5eCharacterCreation : ICharacterCreationStrategy
{
    public string SystemId => "dnd5e";

    public JsonDocument CreateDefaultTemplate()
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            name = "",
            @class = "Fighter",
            level = 1,
            currentHP = 10,
            maxHP = 10,
            background = "Soldier",
            race = "Human",
            alignment = "Neutral",
            attributes = new
            {
                strength = 10, dexterity = 10, constitution = 10,
                intelligence = 10, wisdom = 10, charisma = 10
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
        }));
    }

    public (bool valid, string[] errors) Validate(JsonElement character)
    {
        var errors = new List<string>();

        if (!character.TryGetProperty("name", out var nameProp) || string.IsNullOrEmpty(nameProp.GetString()))
            errors.Add("Name is required.");

        if (!character.TryGetProperty("@class", out var classProp) || string.IsNullOrEmpty(classProp.GetString()))
            errors.Add("Class is required.");

        if (!character.TryGetProperty("attributes", out var attrs))
            errors.Add("Attributes are required.");
        else if (attrs.ValueKind == JsonValueKind.Object)
        {
            var requiredAttrs = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };
            foreach (var attr in requiredAttrs)
            {
                if (!attrs.TryGetProperty(attr, out var val) || val.ValueKind != JsonValueKind.Number)
                    errors.Add($"Attribute '{attr}' is required and must be a number.");
            }
        }

        return (errors.Count == 0, errors.ToArray());
    }

    public Task<Character> InitializeAsync(Character character, AppDbContext context)
    {
        // D&D 5e: saving throws are set on CombatParticipant, not Character
        return Task.FromResult(character);
    }
}

/// <summary>
/// Concrete strategy for Pathfinder 2e character creation.
/// </summary>
public class PF2eCharacterCreation : ICharacterCreationStrategy
{
    public string SystemId => "pf2e";

    public JsonDocument CreateDefaultTemplate()
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            name = "",
            @class = "Fighter",
            ancestry = "Human",
            level = 1,
            currentHP = 12,
            maxHP = 12,
            background = "Soldier",
            attributes = new
            {
                strength = 10, dexterity = 10, constitution = 10,
                intelligence = 10, wisdom = 10, charisma = 10
            },
            skills = new Dictionary<string, int>(),
            proficiency = new { untrained = 0, trained = 2, expert = 4, master = 6, legendary = 8 },
            inventory = Array.Empty<object>(),
            spells = Array.Empty<object>(),
            conditions = Array.Empty<object>(),
            customFields = new Dictionary<string, object>()
        }));
    }

    public (bool valid, string[] errors) Validate(JsonElement character)
    {
        var errors = new List<string>();

        if (!character.TryGetProperty("name", out var nameProp) || string.IsNullOrEmpty(nameProp.GetString()))
            errors.Add("Name is required.");

        if (!character.TryGetProperty("@class", out var classProp) || string.IsNullOrEmpty(classProp.GetString()))
            errors.Add("Class is required.");

        if (!character.TryGetProperty("ancestry", out var ancestryProp) || string.IsNullOrEmpty(ancestryProp.GetString()))
            errors.Add("Ancestry is required for PF2e.");

        return (errors.Count == 0, errors.ToArray());
    }

    public Task<Character> InitializeAsync(Character character, AppDbContext context)
    {
        // PF2e: saving throws handled by combat system
        return Task.FromResult(character);
    }
}

/// <summary>
/// Concrete strategy for Call of Cthulhu 7e character creation.
/// </summary>
public class CoC7eCharacterCreation : ICharacterCreationStrategy
{
    public string SystemId => "coc7e";

    public JsonDocument CreateDefaultTemplate()
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            name = "",
            occupation = "Investigator",
            currentSAN = 60,
            maxSAN = 60,
            currentHP = 12,
            maxHP = 12,
            birthYear = 1920,
            attributes = new
            {
                strength = 50, constitution = 50, size = 50,
                intelligence = 50, power = 50, dexterity = 50, appearance = 50
            },
            skills = new Dictionary<string, int>(),
            inventory = Array.Empty<object>(),
            conditions = Array.Empty<object>(),
            customFields = new Dictionary<string, object>()
        }));
    }

    public (bool valid, string[] errors) Validate(JsonElement character)
    {
        var errors = new List<string>();

        if (!character.TryGetProperty("name", out var nameProp) || string.IsNullOrEmpty(nameProp.GetString()))
            errors.Add("Name is required.");

        if (!character.TryGetProperty("occupation", out var occProp) || string.IsNullOrEmpty(occProp.GetString()))
            errors.Add("Occupation is required for CoC.");

        if (!character.TryGetProperty("attributes", out var attrs))
            errors.Add("Attributes are required.");

        return (errors.Count == 0, errors.ToArray());
    }

    public Task<Character> InitializeAsync(Character character, AppDbContext context)
    {
        // CoC: saving throws handled by combat system
        return Task.FromResult(character);
    }
}

/// <summary>
/// Factory implementation for character creation strategies.
/// </summary>
public class CharacterCreationFactory : ICharacterCreationFactory
{
    private readonly Dictionary<string, ICharacterCreationStrategy> _strategies = new();

    public CharacterCreationFactory()
    {
        RegisterStrategy(new DnD5eCharacterCreation());
        RegisterStrategy(new PF2eCharacterCreation());
        RegisterStrategy(new CoC7eCharacterCreation());
    }

    public ICharacterCreationStrategy GetStrategy(string systemId)
    {
        if (!_strategies.TryGetValue(systemId, out var strategy))
        {
            return _strategies["dnd5e"]; // Fallback to D&D 5e
        }
        return strategy;
    }

    public bool HasStrategy(string systemId) => _strategies.ContainsKey(systemId);

    public void RegisterStrategy(ICharacterCreationStrategy strategy)
    {
        _strategies[strategy.SystemId] = strategy;
    }
}
