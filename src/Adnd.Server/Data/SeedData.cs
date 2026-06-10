using Adnd.Server.Shared;

namespace Adnd.Server.Data;

public static class SeedData
{
    public static readonly GameSystem[] DefaultSystems = new[]
    {
        new GameSystem
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Dungeons & Dragons 5e",
            Slug = "dnd5e",
            Description = "The most popular TTRPG. d20-based system with classes, levels, and spell slots.",
            Type = SystemType.Predefined,
            RulesetConfig = "{\"dicePool\":\"d20\",\"attributes\":[\"STR\",\"DEX\",\"CON\",\"INT\",\"WIS\",\"CHA\"],\"proficiencyBonus\":true}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new GameSystem
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Pathfinder 2e",
            Slug = "pf2e",
            Description = "Strategic TTRPG with proficiency levels (untrained to legendary) and a robust action economy.",
            Type = SystemType.Predefined,
            RulesetConfig = "{\"dicePool\":\"d20\",\"attributes\":[\"STR\",\"DEX\",\"CON\",\"INT\",\"WIS\",\"CHA\",\"CMD\"],\"proficiencyLevels\":true}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new GameSystem
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Call of Cthulhu 7e",
            Slug = "coc7e",
            Description = "Horror TTRPG. d100 skill-based system with sanity tracking and critical/fumble tables.",
            Type = SystemType.Predefined,
            RulesetConfig = "{\"dicePool\":\"d100\",\"attributes\":[\"STR\",\"DEX\",\"CON\",\"APP\",\"INT\",\"POW\",\"SIZ\"],\"sanityTrack\":true}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };
}
