using System.Text.Json;
using Adnd.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public record SpellPatchRequest(string SpellId, JsonElement SpellData);

[ApiController]
[Route("api/spells")]
[Authorize]
public class SpellManagementController(AppDbContext db) : ControllerBase
{
    /// <summary>Returns the Spells jsonb for a character.</summary>
    [HttpGet("{characterId:guid}")]
    public async Task<IActionResult> GetSpells(Guid characterId, CancellationToken ct)
    {
        var character = await db.Characters.FindAsync([characterId], ct);
        if (character == null) return NotFound();
        return Ok(character.Spells);
    }

    /// <summary>Replaces the entire Spells jsonb for a character.</summary>
    [HttpPut("{characterId:guid}")]
    public async Task<IActionResult> UpdateSpells(
        Guid characterId,
        [FromBody] JsonElement spells,
        CancellationToken ct)
    {
        var character = await db.Characters.FindAsync([characterId], ct);
        if (character == null) return NotFound();

        character.Spells = spells;
        await db.SaveChangesAsync(ct);
        return Ok(character.Spells);
    }

    /// <summary>Updates a single spell entry within the Spells jsonb.</summary>
    [HttpPatch("{characterId:guid}/{spellId}")]
    public async Task<IActionResult> PatchSpell(
        Guid characterId,
        string spellId,
        [FromBody] JsonElement spellData,
        CancellationToken ct)
    {
        var character = await db.Characters.FindAsync([characterId], ct);
        if (character == null) return NotFound();

        var spells = character.Spells.ValueKind != JsonValueKind.Undefined
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(character.Spells.GetRawText()) ?? new()
            : new Dictionary<string, JsonElement>();

        spells[spellId] = spellData;
        character.Spells = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(spells));
        await db.SaveChangesAsync(ct);
        return Ok(character.Spells);
    }

    /// <summary>Removes a single spell entry from the Spells jsonb.</summary>
    [HttpDelete("{characterId:guid}/{spellId}")]
    public async Task<IActionResult> DeleteSpell(
        Guid characterId,
        string spellId,
        CancellationToken ct)
    {
        var character = await db.Characters.FindAsync([characterId], ct);
        if (character == null) return NotFound();

        var spells = character.Spells.ValueKind != JsonValueKind.Undefined
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(character.Spells.GetRawText()) ?? new()
            : new Dictionary<string, JsonElement>();

        if (!spells.ContainsKey(spellId)) return NotFound();
        spells.Remove(spellId);
        character.Spells = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(spells));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
