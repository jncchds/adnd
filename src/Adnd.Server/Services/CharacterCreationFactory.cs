using System.Text.Json;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public record BackgroundDefinition(
    string Id,
    string Name,
    string Description,
    string[] SkillProficiencies,
    string[] ToolProficiencies,
    string Feature,
    string SuggestedTrait,
    string SuggestedBond,
    string SuggestedFlaw);

public interface ICharacterCreationFactory
{
    Character CreateFromBackground(string backgroundId, Guid playerId, string name, string className, string? race = null, int level = 1);
    List<BackgroundDefinition> GetAvailableBackgrounds();
    IReadOnlyList<string> GetAvailableRaces();
}

public class CharacterCreationFactory(IFeatureCatalogue features) : ICharacterCreationFactory
{
    /// <summary>
    /// Race exists on the sheet because abilities hang off it — Halfling Luck is a reroll the
    /// server has to know about without being told. The list is not a rules constraint; it is
    /// the set the creation UI offers, and any string is accepted.
    /// </summary>
    private static readonly string[] Races =
    [
        "Human", "Elf", "Dwarf", "Halfling", "Gnome", "Half-Elf", "Half-Orc", "Tiefling", "Dragonborn"
    ];

    public IReadOnlyList<string> GetAvailableRaces() => Races;

    private static readonly List<BackgroundDefinition> Backgrounds =
    [
        new("acolyte", "Acolyte",
            "You have spent your life in service to a temple.",
            ["Insight", "Religion"],
            ["Two languages of your choice"],
            "Shelter of the Faithful",
            "I idolize a particular hero of my faith.",
            "I owe my life to the priest who took me in after my parents died.",
            "I am inflexible in my thinking."),

        new("criminal", "Criminal",
            "You are an experienced criminal with a history of breaking the law.",
            ["Deception", "Stealth"],
            ["One type of gaming set", "Thieves' tools"],
            "Criminal Contact",
            "I always have a plan for when things go wrong.",
            "I'm trying to pay off an old debt I owe to a generous benefactor.",
            "When I see something valuable, I can't think of anything but how to steal it."),

        new("soldier", "Soldier",
            "War has been your life for as long as you care to remember.",
            ["Athletics", "Intimidation"],
            ["One type of gaming set", "Vehicles (land)"],
            "Military Rank",
            "I'm always polite and respectful.",
            "Those who fight beside me are those worth dying for.",
            "I made a terrible mistake in battle that cost many lives."),

        new("sage", "Sage",
            "You spent years learning the lore of the multiverse.",
            ["Arcana", "History"],
            ["Two languages of your choice"],
            "Researcher",
            "I use polysyllabic words to convey the impression of great erudition.",
            "I work to preserve a library, university, or scriptorium.",
            "I speak without really thinking through my words, invariably insulting others."),

        new("gladiator", "Gladiator",
            "You have trained as a gladiator and know the thrill of the arena.",
            ["Athletics", "Performance"],
            ["One type of musical instrument", "Unusual weapon"],
            "By Popular Demand",
            "I know I'm the best. The sooner others come to accept that, the better.",
            "My instrument is my most treasured possession.",
            "I have trouble keeping my true feelings hidden."),

        new("folkhero", "Folk Hero",
            "You come from a humble social rank, but you are destined for much more.",
            ["Animal Handling", "Survival"],
            ["One type of artisan's tools", "Vehicles (land)"],
            "Rustic Hospitality",
            "I judge people by their actions, not their words.",
            "I protect those who cannot protect themselves.",
            "The tyrant who rules my land will stop at nothing to see me captured or dead."),

        new("urchin", "Urchin",
            "You grew up on the streets alone, orphaned and poor.",
            ["Sleight of Hand", "Stealth"],
            ["Disguise kit", "Thieves' tools"],
            "City Secrets",
            "I hide scraps of food and trinkets away in my pockets.",
            "I escaped my life of poverty by robbing an important person.",
            "Gold seems like a lot of money to me, and I'll do just about anything for more."),

        new("noble", "Noble",
            "You understand wealth, power, and privilege.",
            ["History", "Persuasion"],
            ["One type of gaming set", "One language of your choice"],
            "Position of Privilege",
            "My eloquent flattery makes everyone I talk to feel like the most wonderful person in the world.",
            "I will face any challenge to win the approval of my family.",
            "I secretly believe that everyone is beneath me.")
    ];

    public List<BackgroundDefinition> GetAvailableBackgrounds() => Backgrounds;

    public Character CreateFromBackground(string backgroundId, Guid playerId, string name, string className, string? race = null, int level = 1)
    {
        var bg = Backgrounds.FirstOrDefault(b => b.Id.Equals(backgroundId, StringComparison.OrdinalIgnoreCase))
                 ?? Backgrounds[0]; // default to Acolyte

        var proficiencyBonus = level switch
        {
            <= 4 => 2,
            <= 8 => 3,
            <= 12 => 4,
            <= 16 => 5,
            _ => 6
        };

        var defaultAttributes = new Dictionary<string, int>
        {
            ["STR"] = 10, ["DEX"] = 10, ["CON"] = 10,
            ["INT"] = 10, ["WIS"] = 10, ["CHA"] = 10
        };

        var skillsDict = bg.SkillProficiencies.ToDictionary(s => s, _ => true);

        var defaultInventory = new[] { "Clothes", "Backpack", "Rations (5 days)", "Waterskin", "Tinderbox" };

        // Granted up front rather than asked for: a Halfling player should not have to know
        // that Halfling Luck is the thing that makes the reroll prompt appear.
        var granted = features.GrantedTo(race, className, level)
            .Select(d => new CharacterFeature(d.Id, d.Name, d.MaxUses))
            .ToList();

        return new Character
        {
            PlayerId = playerId,
            Name = name,
            Class = className,
            Race = race,
            Level = level,
            ProficiencyBonus = proficiencyBonus,
            CurrentHP = 10 + level,
            MaxHP = 10 + level,
            Attributes = JsonSerializer.SerializeToElement(defaultAttributes),
            Skills = JsonSerializer.SerializeToElement(skillsDict),
            Inventory = JsonSerializer.SerializeToElement(defaultInventory),
            Spells = JsonSerializer.SerializeToElement(new object()),
            Conditions = JsonSerializer.SerializeToElement(Array.Empty<string>()),
            CustomFields = JsonSerializer.SerializeToElement(new object()),
            SpellSlots = JsonSerializer.SerializeToElement(new object()),
            Features = JsonSerializer.SerializeToElement(granted, FeatureJson.Options),
            Background = bg.Description,
            BackgroundSkills = string.Join(", ", bg.SkillProficiencies),
            BackgroundProficiencies = string.Join(", ", bg.ToolProficiencies),
            BackgroundFeatures = bg.Feature
        };
    }
}
