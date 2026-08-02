using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[Route("api/[controller]")]
public class NPCsController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (await RequireMember(gameId) is { } failure) return failure;

        var npcs = await db.NPCs.AsNoTracking().Where(n => n.GameId == gameId).ToListAsync(ct);
        return Ok(npcs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var npc = await db.NPCs.FindAsync(id);
        if (npc == null) return NotFound();

        // Authorize against the NPC's own game, not one the caller claims.
        if (await RequireMember(npc.GameId) is { } failure) return failure;

        return Ok(npc);
    }

    // NPCs are the GM's to author. Binding the NPC entity directly also let the caller
    // choose Id/GameId/IsDeleted, so these take DTOs.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNPCDto dto)
    {
        if (await RequireCreator(dto.GameId) is { } failure) return failure;

        var npc = new NPC
        {
            GameId = dto.GameId,
            Name = dto.Name,
            Description = dto.Description,
            Attitude = dto.Attitude,
            Faction = dto.Faction,
            // An NPC the GM just typed in is the most current one there is; without this it
            // sorts below every LLM-registered NPC in the relevance roster.
            LastSeenAt = DateTimeOffset.UtcNow
        };

        db.NPCs.Add(npc);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = npc.Id }, npc);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNPCDto dto)
    {
        var npc = await db.NPCs.FindAsync(id);
        if (npc == null) return NotFound();
        if (await RequireCreator(npc.GameId) is { } failure) return failure;

        if (dto.Name is not null) npc.Name = dto.Name;
        if (dto.Description is not null) npc.Description = dto.Description;
        if (dto.Attitude.HasValue) npc.Attitude = dto.Attitude.Value;
        if (dto.Faction is not null) npc.Faction = dto.Faction;
        if (dto.Status.HasValue) npc.Status = dto.Status.Value;

        await db.SaveChangesAsync();
        return Ok(npc);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var npc = await db.NPCs.FindAsync(id);
        if (npc == null) return NotFound();
        if (await RequireCreator(npc.GameId) is { } failure) return failure;

        npc.IsDeleted = true;
        npc.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
