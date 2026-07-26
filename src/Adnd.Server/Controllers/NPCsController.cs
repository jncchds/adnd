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
public class NPCsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid gameId)
    {
        var npcs = await db.NPCs.Where(n => n.GameId == gameId).ToListAsync();
        return Ok(npcs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var npc = await db.NPCs.FindAsync(id);
        if (npc == null) return NotFound();
        return Ok(npc);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NPC npc)
    {
        db.NPCs.Add(npc);
        await db.SaveChangesAsync();
        return Ok(npc);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] NPC update)
    {
        var npc = await db.NPCs.FindAsync(id);
        if (npc == null) return NotFound();
        npc.Name = update.Name;
        npc.Description = update.Description;
        npc.Attitude = update.Attitude;
        npc.Faction = update.Faction;
        await db.SaveChangesAsync();
        return Ok(npc);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var npc = await db.NPCs.FindAsync(id);
        if (npc == null) return NotFound();
        npc.IsDeleted = true;
        npc.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
