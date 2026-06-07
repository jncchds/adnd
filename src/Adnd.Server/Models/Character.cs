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
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
