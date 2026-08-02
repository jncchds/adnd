using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public record SpellPatchRequest(string SpellId, JsonElement SpellData);

[Route("api/spells")]
public class SpellManagementController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    /// <summary>Returns the Spells jsonb for a character.</summary>
    [HttpGet("{characterId:guid}")]
    public async Task<IActionResult> GetSpells(Guid characterId, CancellationToken ct)
    {
        var (character, failure) = await LoadAuthorizedCharacterAsync(characterId, ct);
        if (failure is not null) return failure;
        return Ok(character!.Spells);
    }

    /// <summary>Replaces the entire Spells jsonb for a character.</summary>
    [HttpPut("{characterId:guid}")]
    public async Task<IActionResult> UpdateSpells(Guid characterId, [FromBody] JsonElement spells, CancellationToken ct)
    {
        var (character, failure) = await LoadAuthorizedCharacterAsync(characterId, ct);
        if (failure is not null) return failure;

        character!.Spells = spells;
        await db.SaveChangesAsync(ct);
        return Ok(character.Spells);
    }

    /// <summary>Updates a single spell entry within the Spells jsonb.</summary>
    [HttpPatch("{characterId:guid}/{spellId}")]
    public async Task<IActionResult> PatchSpell(Guid characterId, string spellId, [FromBody] JsonElement spellData, CancellationToken ct)
    {
        var (character, failure) = await LoadAuthorizedCharacterAsync(characterId, ct);
        if (failure is not null) return failure;

        var sheet = character!;
        var spells = ReadSpells(sheet);
        spells[spellId] = spellData;
        sheet.Spells = JsonSerializer.SerializeToElement(spells);
        await db.SaveChangesAsync(ct);
        return Ok(sheet.Spells);
    }

    /// <summary>Removes a single spell entry from the Spells jsonb.</summary>
    [HttpDelete("{characterId:guid}/{spellId}")]
    public async Task<IActionResult> DeleteSpell(Guid characterId, string spellId, CancellationToken ct)
    {
        var (character, failure) = await LoadAuthorizedCharacterAsync(characterId, ct);
        if (failure is not null) return failure;

        var sheet = character!;
        var spells = ReadSpells(sheet);
        if (!spells.Remove(spellId)) return NotFound();

        sheet.Spells = JsonSerializer.SerializeToElement(spells);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// A character's spell list is editable by the player who owns it and by the GM.
    /// These endpoints previously accepted any character id from any caller, so anyone
    /// could rewrite anyone's spells.
    /// </summary>
    private async Task<(Character? Character, IActionResult? Failure)> LoadAuthorizedCharacterAsync(
        Guid characterId, CancellationToken ct)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct);
        if (character == null) return (null, NotFound());

        // A Character reaches its game through Player, and Player has a soft-delete filter
        // that Character does not — so a kicked player's sheet would otherwise resolve to a
        // null Player and 500. Read past the filter to find the owning game.
        var owner = await db.Players
            .IgnoreQueryFilters()
            .Where(p => p.Id == character.PlayerId)
            .Select(p => new { p.UserId, p.GameId })
            .FirstOrDefaultAsync(ct);

        if (owner is null) return (null, NotFound());

        if (owner.UserId == CurrentUserId) return (character, null);

        if (await RequireCreator(owner.GameId) is { } failure) return (null, failure);
        return (character, null);
    }

    private static Dictionary<string, JsonElement> ReadSpells(Character character)
        => character.Spells.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(character.Spells.GetRawText()) ?? []
            : [];
}
