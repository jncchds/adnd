using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Events;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Character Management ====================

    [HttpGet("games/{gameId}/characters")]
    public async Task<IActionResult> GetCharacters(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var characters = await _context.Characters
            .Where(c => c.Player!.GameId == gameId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Class,
                c.Level,
                c.ProficiencyBonus,
                c.CurrentHP,
                c.MaxHP,
                c.Attributes,
                c.Skills,
                c.Inventory,
                c.Spells,
                c.Conditions,
                c.CustomFields,
                c.UpdatedAt,
                PlayerName = c.Player!.User != null ? (c.Player.User!.DisplayName ?? c.Player.CharacterName) : c.Player.CharacterName,
                PlayerUserId = c.Player!.UserId.ToString(),
                PlayerEmail = c.Player!.User != null ? c.Player.User.Email : null
            })
            .ToListAsync();

        return Ok(characters);
    }

    [HttpGet("characters/{characterId}")]
    public async Task<IActionResult> GetCharacter(Guid characterId)
    {
        var character = await _context.Characters
            .Include(c => c.Player!)
            .ThenInclude(p => p.User!)
            .FirstOrDefaultAsync(c => c.Id == characterId);

        if (character == null) return NotFound(new { error = "Character not found." });

        var playerId = character.Player?.GameId;
        if (playerId == null) return NotFound(new { error = "Character player not found." });

        var game = await _context.Games.FindAsync(playerId.Value);
        if (game == null || !await _authService.HasAccessAsync(_context, playerId.Value, _userIdProvider.GetCurrentUserId())) return Forbid();

        return Ok(new
        {
            character.Id,
            character.Name,
            character.Class,
            character.Level,
            character.CurrentHP,
            character.MaxHP,
            character.Attributes,
            character.Skills,
            character.Inventory,
            character.Spells,
            character.Conditions,
            character.CustomFields,
            character.UpdatedAt,
            PlayerName = character.Player?.User?.DisplayName ?? character.Player!.CharacterName
        });
    }

    [HttpPut("characters/{characterId}")]
    public async Task<IActionResult> UpdateCharacter(Guid characterId, [FromBody] UpdateCharacterRequest request)
    {
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Character not found." });

        var game = await _context.Games.FindAsync(character.Player!.GameId);
        if (game == null || !await _authService.HasAccessAsync(_context, character.Player.GameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        if (request.Name != null) character.Name = request.Name;
        if (request.Class != null) character.Class = request.Class;
        if (request.Level != null)
        {
            character.Level = (int)request.Level;
            character.ProficiencyBonus = GetProficiencyBonus((int)request.Level);
        }
        if (request.CurrentHP != null) character.CurrentHP = (int)request.CurrentHP;
        if (request.MaxHP != null) character.MaxHP = (int)request.MaxHP;
        if (request.Attributes != null) character.Attributes = request.Attributes.GetValueOrDefault();
        if (request.Skills != null) character.Skills = request.Skills.GetValueOrDefault();
        if (request.Inventory != null) character.Inventory = request.Inventory.GetValueOrDefault();
        if (request.Spells != null) character.Spells = request.Spells.GetValueOrDefault();
        if (request.Conditions != null) character.Conditions = request.Conditions.GetValueOrDefault();
        character.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new CharacterUpdated(character.Player!.GameId, character.Id));

        return Ok(new { character.Id, character.Name, character.Class, character.Level });
    }

}
