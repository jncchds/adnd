using System.Text.Json;

namespace Adnd.Server.Dtos;

public record CreateCharacterDto(
    Guid GameId,
    string Name,
    string Class,
    string Background,
    string? Backstory,
    Dictionary<string, int>? Attributes,
    string? Race);

/// <summary>
/// Fields a player may edit on their own sheet. Update previously bound the Character
/// entity itself, so the client could set its own Level, MaxHP and CurrentHP — a fully
/// client-authoritative character sheet. Progression-affecting fields are GM-only
/// (see <see cref="GMCharacterAdjustmentDto"/>).
/// </summary>
public record UpdateCharacterDto(
    string? Name,
    string? Class,
    string? Background,
    string? Backstory,
    JsonElement? Attributes,
    JsonElement? Skills,
    JsonElement? Inventory,
    JsonElement? Spells,
    JsonElement? Conditions,
    JsonElement? CustomFields,
    JsonElement? SpellSlots,
    string? Race,
    JsonElement? Features);

/// <summary>GM-only adjustments to a character's progression and current state.</summary>
public record GMCharacterAdjustmentDto(
    int? Level,
    int? CurrentHP,
    int? MaxHP,
    int? ProficiencyBonus);
