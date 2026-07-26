using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CharactersController(AppDbContext db) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var character = await db.Characters.Include(c => c.Player).FirstOrDefaultAsync(c => c.Id == id);
        if (character == null) return NotFound();
        return Ok(character);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Character update)
    {
        var character = await db.Characters.FindAsync(id);
        if (character == null) return NotFound();
        character.Name = update.Name;
        character.Class = update.Class;
        character.Level = update.Level;
        character.CurrentHP = update.CurrentHP;
        character.MaxHP = update.MaxHP;
        character.Attributes = update.Attributes;
        character.Skills = update.Skills;
        character.Inventory = update.Inventory;
        character.Spells = update.Spells;
        character.Conditions = update.Conditions;
        character.CustomFields = update.CustomFields;
        character.Background = update.Background;
        character.SpellSlots = update.SpellSlots;
        await db.SaveChangesAsync();
        return Ok(character);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Character character)
    {
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return Ok(character);
    }
}
