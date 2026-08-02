using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CharactersController(
    AppDbContext db,
    ICharacterCreationFactory factory,
    IUserIdProvider userIdProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListForGame([FromQuery] Guid gameId)
    {
        var userId = userIdProvider.GetUserId();
        var isAuthorized =
            await db.Players.IgnoreQueryFilters().AnyAsync(p => p.GameId == gameId && p.UserId == userId && !p.IsDeleted) ||
            await db.Games.IgnoreQueryFilters().AnyAsync(g => g.Id == gameId && g.CreatorId == userId && !g.IsDeleted);
        if (!isAuthorized) return Forbid();
        var characters = await db.Characters
            .Where(c => c.Player.GameId == gameId)
            .ToListAsync();
        return Ok(characters);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy([FromQuery] Guid gameId)
    {
        var userId = userIdProvider.GetUserId();
        var player = await db.Players.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && !p.IsDeleted);
        if (player == null)
        {
            var isCreator = await db.Games.IgnoreQueryFilters()
                .AnyAsync(g => g.Id == gameId && g.CreatorId == userId && !g.IsDeleted);
            return isCreator ? NoContent() : NotFound();
        }
        var character = await db.Characters
            .FirstOrDefaultAsync(c => c.PlayerId == player.Id);
        if (character == null) return NoContent();
        return Ok(character);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null) return NotFound();

        if (!await CanAccessCharacterAsync(character)) return Forbid();
        return Ok(character);
    }

    /// <summary>
    /// A sheet is readable by the player who owns it and by the game's creator.
    /// This used to test only Player.UserId, which (a) locked the GM out of their own
    /// players' sheets — ListForGame already allowed it — and (b) dereferenced Player,
    /// which is soft-deleted independently of Character and so is null for a kicked
    /// player's sheet, producing a 500.
    /// </summary>
    private async Task<bool> CanAccessCharacterAsync(Character character)
    {
        var userId = userIdProvider.GetUserId();

        var owner = await db.Players
            .IgnoreQueryFilters()
            .Where(p => p.Id == character.PlayerId)
            .Select(p => new { p.UserId, p.GameId })
            .FirstOrDefaultAsync();

        if (owner is null) return false;
        if (owner.UserId == userId) return true;

        return await db.Games
            .IgnoreQueryFilters()
            .AnyAsync(g => g.Id == owner.GameId && g.CreatorId == userId && !g.IsDeleted);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCharacterDto dto)
    {
        var userId = userIdProvider.GetUserId();

        var player = await db.Players
            .Include(p => p.Character)
            .FirstOrDefaultAsync(p => p.GameId == dto.GameId && p.UserId == userId);
        if (player == null)
            return BadRequest("You are not a player in this game.");

        if (player.Character != null)
            return Conflict("You already have a character in this game.");

        var character = factory.CreateFromBackground(dto.Background, player.Id, dto.Name, dto.Class);

        if (dto.Attributes is { Count: > 0 })
            character.Attributes = JsonSerializer.SerializeToElement(dto.Attributes);

        character.Backstory = dto.Backstory;

        player.CharacterName = character.Name;
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = character.Id }, character);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCharacterDto dto)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null) return NotFound();
        if (!await CanAccessCharacterAsync(character)) return Forbid();

        if (dto.Name is not null) character.Name = dto.Name;
        if (dto.Class is not null) character.Class = dto.Class;
        if (dto.Background is not null) character.Background = dto.Background;
        if (dto.Backstory is not null) character.Backstory = dto.Backstory;
        if (dto.Attributes.HasValue) character.Attributes = dto.Attributes.Value;
        if (dto.Skills.HasValue) character.Skills = dto.Skills.Value;
        if (dto.Inventory.HasValue) character.Inventory = dto.Inventory.Value;
        if (dto.Spells.HasValue) character.Spells = dto.Spells.Value;
        if (dto.Conditions.HasValue) character.Conditions = dto.Conditions.Value;
        if (dto.CustomFields.HasValue) character.CustomFields = dto.CustomFields.Value;
        if (dto.SpellSlots.HasValue) character.SpellSlots = dto.SpellSlots.Value;

        await db.SaveChangesAsync();
        return Ok(character);
    }

    /// <summary>
    /// Level and hit points are GM-controlled — a player editing their own sheet cannot
    /// set them, which is why they are split out of <see cref="UpdateCharacterDto"/>.
    /// </summary>
    [HttpPatch("{id:guid}/adjust")]
    public async Task<IActionResult> Adjust(Guid id, [FromBody] GMCharacterAdjustmentDto dto)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null) return NotFound();

        var owner = await db.Players
            .IgnoreQueryFilters()
            .Where(p => p.Id == character.PlayerId)
            .Select(p => new { p.GameId })
            .FirstOrDefaultAsync();

        if (owner is null) return NotFound();

        var userId = userIdProvider.GetUserId();
        var isCreator = await db.Games
            .IgnoreQueryFilters()
            .AnyAsync(g => g.Id == owner.GameId && g.CreatorId == userId && !g.IsDeleted);

        if (!isCreator) return Forbid();

        if (dto.Level.HasValue) character.Level = dto.Level.Value;
        if (dto.MaxHP.HasValue) character.MaxHP = dto.MaxHP.Value;
        if (dto.CurrentHP.HasValue) character.CurrentHP = Math.Clamp(dto.CurrentHP.Value, 0, character.MaxHP);
        if (dto.ProficiencyBonus.HasValue) character.ProficiencyBonus = dto.ProficiencyBonus.Value;

        await db.SaveChangesAsync();
        return Ok(character);
    }
}
