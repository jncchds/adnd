using System.Text.Json;

namespace Adnd.Server.Models;

public class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; }
    public int ProficiencyBonus { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public JsonElement Attributes { get; set; } // System-specific stats
    public JsonElement Skills { get; set; }     // Skill values
    public JsonElement Inventory { get; set; }   // Items
    public JsonElement Spells { get; set; }      // Spells
    public JsonElement Conditions { get; set; }  // Active conditions
    public JsonElement CustomFields { get; set; } // Anything else

    // Background / Archetype
    public string? Background { get; set; }
    public JsonElement? BackgroundSkills { get; set; } // Selected background skills
    public JsonElement? BackgroundProficiencies { get; set; } // Additional proficiencies
    public JsonElement? BackgroundFeatures { get; set; } // Background features/traits

    // Spell slot tracking (D&D 5e style)
    public JsonElement? SpellSlots { get; set; } // { "1": { "total": 4, "remaining": 4 }, "2": { ... }, ... }
    public JsonElement? SpellcastingAbility { get; set; } // Which attribute is used for spellcasting
    public int? SpellSaveDC { get; set; }
    public int? SpellAttackBonus { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
