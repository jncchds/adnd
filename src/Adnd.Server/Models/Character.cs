using System.Text.Json;

namespace Adnd.Server.Models;

public class Character : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string? Race { get; set; }
    public int Level { get; set; } = 1;
    public int ProficiencyBonus { get; set; } = 2;
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }

    public JsonElement Attributes { get; set; }
    public JsonElement Skills { get; set; }
    public JsonElement Inventory { get; set; }
    public JsonElement Spells { get; set; }
    public JsonElement Conditions { get; set; }
    public JsonElement CustomFields { get; set; }

    /// <summary>
    /// Abilities the character has, as <c>[{ "id", "name", "usesRemaining" }]</c>. Only the
    /// id and the remaining uses are authoritative here — what the ability *does* (its
    /// trigger, its maximum, which rest restores it) lives in
    /// <see cref="Services.IFeatureCatalogue"/>, so a rules fix does not have to be
    /// backfilled across every character row. The name is denormalised for display only,
    /// so a homebrew id the catalogue doesn't know still renders as something readable.
    /// </summary>
    public JsonElement Features { get; set; }

    public string? Background { get; set; }
    public string? BackgroundSkills { get; set; }
    public string? BackgroundProficiencies { get; set; }
    public string? BackgroundFeatures { get; set; }

    public JsonElement SpellSlots { get; set; }
    public string? SpellcastingAbility { get; set; }
    public int SpellSaveDC { get; set; }
    public int SpellAttackBonus { get; set; }

    public string? Backstory { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Player Player { get; set; } = null!;
}
