using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>
/// Renders a Character/NPC's raw Attributes jsonb blob (keyed "STR"/"DEX"/... to a raw score,
/// set by CharacterCreateWizard) into D&D 5e-style modifiers the GM LLM can drop straight into
/// a dice formula. Without this the LLM had no numeric modifier to work with and would invent
/// placeholder formulas like "1d20+{strength}", which DiceEngine can't resolve.
/// </summary>
public static class AbilityScoreHelper
{
    private static readonly string[] Order = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    public static int GetModifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    public static string FormatModifiers(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object) return string.Empty;

        var parts = new List<string>();
        foreach (var key in Order)
        {
            if (attributes.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Number)
            {
                var mod = GetModifier(val.GetInt32());
                parts.Add($"{key} {(mod >= 0 ? "+" : "")}{mod}");
            }
        }
        return string.Join(", ", parts);
    }
}
