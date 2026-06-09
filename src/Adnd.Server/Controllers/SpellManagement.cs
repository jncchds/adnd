using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Data;
using Adnd.Server.Services;
using System.Text.Json;

namespace Adnd.Server.Controllers;

/// <summary>
/// Spell management for characters.
/// Provides structured access to character spells and spell slots.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SpellManagementController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGameAuthorizationService _authService;
    private readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    private readonly ILogger<SpellManagementController> _logger;

    public SpellManagementController(
        AppDbContext context,
        IGameAuthorizationService authService,
        Adnd.Server.Services.IUserIdProvider userIdProvider,
        ILogger<SpellManagementController> logger)
    {
        _context = context;
        _authService = authService;
        _userIdProvider = userIdProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get structured spell data for a character.
    /// Parses the JSON spells field into a typed list.
    /// </summary>
    [HttpGet("characters/{characterId}/spells")]
    public async Task<IActionResult> GetCharacterSpells(Guid characterId)
    {
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Character not found." });

        var game = await _context.Games.FindAsync(character.Player!.GameId);
        if (game == null || !await _authService.HasAccessAsync(_context, character.Player.GameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var spells = ParseSpells(character.Spells);
        var slots = ParseSpellSlots(character.Spells);

        return Ok(new
        {
            characterId,
            characterName = character.Name,
            characterClass = character.Class,
            characterLevel = character.Level,
            spells,
            spellSlots = slots,
            updatedAt = character.UpdatedAt
        });
    }

    /// <summary>
    /// Update character spells. Accepts a list of spell entries.
    /// </summary>
    [HttpPut("characters/{characterId}/spells")]
    public async Task<IActionResult> UpdateCharacterSpells(Guid characterId, [FromBody] List<SpellUpdateRequest> requests)
    {
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Character not found." });

        var game = await _context.Games.FindAsync(character.Player!.GameId);
        if (game == null || !await _authService.HasAccessAsync(_context, character.Player.GameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        // Merge spell data
        var spellsJson = JsonSerializer.Serialize(requests);
        character.Spells = JsonDocument.Parse(spellsJson).RootElement;

        // Update spell slot usage if provided
        if (requests.Any(r => r.SpellSlots != null))
        {
            var slots = requests.First(r => r.SpellSlots != null).SpellSlots;
            character.CustomFields = JsonSerializer.SerializeToElement(new { spellSlots = slots });
        }

        character.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            characterId,
            spellCount = requests.Count,
            updatedAt = character.UpdatedAt
        });
    }

    /// <summary>
    /// Update a single spell entry.
    /// </summary>
    [HttpPut("characters/{characterId}/spells/{spellName}")]
    public async Task<IActionResult> UpdateSpell(Guid characterId, string spellName, [FromBody] SpellUpdateRequest request)
    {
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Character not found." });

        var game = await _context.Games.FindAsync(character.Player!.GameId);
        if (game == null || !await _authService.HasAccessAsync(_context, character.Player.GameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var spells = ParseSpells(character.Spells);
        var spell = spells.FirstOrDefault(s => s.Name.Equals(spellName, StringComparison.OrdinalIgnoreCase));

        if (spell == null)
        {
            // Add new spell
            spells.Add(new SpellEntry
            {
                Name = request.Name ?? spellName,
                Level = request.Level ?? "0",
                School = request.School ?? "",
                CastingTime = request.CastingTime ?? "1 action",
                Range = request.Range ?? "Self",
                Duration = request.Duration ?? "Instantaneous",
                Components = request.Components ?? "V,S",
                Description = request.Description ?? "",
                SaveType = request.SaveType,
                SaveDC = request.SaveDC,
                DamageFormula = request.DamageFormula,
                DamageBonus = request.DamageBonus,
                DamageType = request.DamageType,
                IsPrepared = request.IsPrepared ?? true,
                IsKnown = request.IsKnown ?? true
            });
        }
        else
        {
            // Update existing spell
            if (request.Level != null) spell.Level = request.Level;
            if (request.School != null) spell.School = request.School;
            if (request.CastingTime != null) spell.CastingTime = request.CastingTime;
            if (request.Range != null) spell.Range = request.Range;
            if (request.Duration != null) spell.Duration = request.Duration;
            if (request.Components != null) spell.Components = request.Components;
            if (request.Description != null) spell.Description = request.Description;
            if (request.SaveType != null) spell.SaveType = request.SaveType;
            if (request.SaveDC != null) spell.SaveDC = request.SaveDC;
            if (request.DamageFormula != null) spell.DamageFormula = request.DamageFormula;
            if (request.DamageBonus != null) spell.DamageBonus = request.DamageBonus;
            if (request.DamageType != null) spell.DamageType = request.DamageType;
            if (request.IsPrepared != null) spell.IsPrepared = request.IsPrepared.Value;
            if (request.IsKnown != null) spell.IsKnown = request.IsKnown.Value;
        }

        character.Spells = JsonSerializer.SerializeToElement(spells);
        character.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { characterId, spellName = spell.Name, updatedAt = character.UpdatedAt });
    }

    /// <summary>
    /// Remove a spell from a character.
    /// </summary>
    [HttpDelete("characters/{characterId}/spells/{spellName}")]
    public async Task<IActionResult> RemoveSpell(Guid characterId, string spellName)
    {
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Character not found." });

        var game = await _context.Games.FindAsync(character.Player!.GameId);
        if (game == null || !await _authService.HasAccessAsync(_context, character.Player.GameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var spells = ParseSpells(character.Spells);
        var removed = spells.RemoveAll(s => s.Name.Equals(spellName, StringComparison.OrdinalIgnoreCase));

        if (removed == 0) return NotFound(new { error = $"Spell '{spellName}' not found." });

        character.Spells = JsonSerializer.SerializeToElement(spells);
        character.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { characterId, spellName, removed });
    }

    // ==================== Helpers ====================

    private List<SpellEntry> ParseSpells(JsonElement spellsJson)
    {
        try
        {
            if (spellsJson.ValueKind == JsonValueKind.Array)
            {
                return spellsJson.EnumerateArray()
                    .Select(s => new SpellEntry
                    {
                        Name = s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Level = s.TryGetProperty("level", out var l) ? l.GetString() ?? "0" : "0",
                        School = s.TryGetProperty("school", out var sc) ? sc.GetString() ?? "" : "",
                        CastingTime = s.TryGetProperty("castingTime", out var ct) ? ct.GetString() ?? "1 action" : "1 action",
                        Range = s.TryGetProperty("range", out var r) ? r.GetString() ?? "Self" : "Self",
                        Duration = s.TryGetProperty("duration", out var d) ? d.GetString() ?? "Instantaneous" : "Instantaneous",
                        Components = s.TryGetProperty("components", out var comp) ? comp.GetString() ?? "V,S" : "V,S",
                        Description = s.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        SaveType = s.TryGetProperty("saveType", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null,
                        SaveDC = s.TryGetProperty("saveDC", out var sd) && sd.ValueKind == JsonValueKind.Number ? sd.GetInt32() : (int?)null,
                        DamageFormula = s.TryGetProperty("damageFormula", out var df) && df.ValueKind == JsonValueKind.String ? df.GetString() : null,
                        DamageBonus = s.TryGetProperty("damageBonus", out var db) && db.ValueKind == JsonValueKind.Number ? db.GetInt32() : (int?)null,
                        DamageType = s.TryGetProperty("damageType", out var dt) && dt.ValueKind == JsonValueKind.String ? dt.GetString() : null,
                        IsPrepared = s.TryGetProperty("isPrepared", out var ip) ? ip.GetBoolean() : true,
                        IsKnown = s.TryGetProperty("isKnown", out var ik) ? ik.GetBoolean() : true
                    })
                    .ToList();
            }
        }
        catch { /* return empty list on parse error */ }
        return new List<SpellEntry>();
    }

    private List<SpellSlotInfo> ParseSpellSlots(JsonElement spellsJson)
    {
        try
        {
            if (spellsJson.ValueKind == JsonValueKind.Array)
            {
                var slotsProp = spellsJson.GetProperty("spellSlots");
                if (slotsProp.ValueKind == JsonValueKind.Array)
                {
                    return slotsProp.EnumerateArray()
                        .Select(s => new SpellSlotInfo
                        {
                            Level = s.TryGetProperty("level", out var l) ? l.GetString() ?? "1" : "1",
                            SlotsTotal = s.TryGetProperty("slotsTotal", out var st) ? st.GetInt32() : 2,
                            SlotsRemaining = s.TryGetProperty("slotsRemaining", out var sr) ? sr.GetInt32() : 2,
                            SlotsUsed = s.TryGetProperty("slotsUsed", out var su) && su.ValueKind == JsonValueKind.Number ? su.GetInt32() : 0
                        })
                        .ToList();
                }
            }
        }
        catch { /* return empty list on parse error */ }
        return new List<SpellSlotInfo>();
    }
}

public class SpellUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Level { get; set; }
    public string? School { get; set; }
    public string? CastingTime { get; set; }
    public string? Range { get; set; }
    public string? Duration { get; set; }
    public string? Components { get; set; }
    public string? Description { get; set; }
    public string? SaveType { get; set; }
    public int? SaveDC { get; set; }
    public string? DamageFormula { get; set; }
    public int? DamageBonus { get; set; }
    public string? DamageType { get; set; }
    public bool? IsPrepared { get; set; }
    public bool? IsKnown { get; set; }
    public List<SpellSlotInfo>? SpellSlots { get; set; }
}
