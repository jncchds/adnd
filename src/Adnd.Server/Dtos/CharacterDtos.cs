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
/// Asks the GM's model to fill in the rest of a character. Whatever the player has already
/// typed is sent along and comes back unchanged; both empty is the "surprise me" path.
/// </summary>
public record SuggestCharacterDto(
    Guid GameId,
    string? Name,
    string? Backstory);

/// <summary>
/// Fields a player may edit on their own sheet: who the character is, never what they can do.
/// Everything mechanical — attributes, hit points, inventory, spells, slots, abilities,
/// level — is changed only by the GM's tools, because the LLM runs the game. This DTO used
/// to accept attributes, inventory, spell slots and features, so a player could grant
/// themselves Lucky from the sheet; and BackgroundSkills/BackgroundFeatures, which the sheet
/// does edit, were missing, so those edits were silently dropped.
/// </summary>
public record UpdateCharacterDto(
    string? Name,
    string? Background,
    string? BackgroundSkills,
    string? BackgroundFeatures,
    string? Backstory,
    JsonElement? CustomFields);
