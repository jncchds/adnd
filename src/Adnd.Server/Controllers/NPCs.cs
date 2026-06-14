using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Events;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== NPC Management ====================

    [HttpGet("games/{gameId}/npcs")]
    public async Task<IActionResult> GetNPCs(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });

        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var npcs = await _context.NPCs
            .Where(n => n.GameId == gameId)
            .Select(n => new
            {
                n.Id,
                n.Name,
                n.Description,
                n.Attributes,
                n.Skills,
                n.Inventory,
                n.Spells,
                n.PlotThreadId,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(npcs);
    }

    [HttpPost("games/{gameId}/npcs")]
    public async Task<IActionResult> CreateNPC(Guid gameId, [FromBody] CreateNPCRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var npc = new NPC
        {
            GameId = gameId,
            Name = request.Name,
            Description = request.Description,
            Attributes = request.Attributes ?? default,
            Skills = request.Skills ?? default,
            Inventory = request.Inventory ?? default,
            Spells = request.Spells ?? default,
            PlotThreadId = request.PlotThreadId,
            CreatedAt = DateTime.UtcNow
        };

        _context.NPCs.Add(npc);
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new NPCCreated(gameId, npc.Id, npc.Name));

        return Ok(new { npc.Id, npc.Name, npc.Description });
    }

    [HttpPut("npcs/{npcId}")]
    public async Task<IActionResult> UpdateNPC(Guid npcId, [FromBody] UpdateNPCRequest request)
    {
        var npc = await _context.NPCs.FindAsync(npcId);
        if (npc == null) return NotFound(new { error = "NPC not found." });

        var game = await _context.Games.FindAsync(npc.GameId);
        if (game == null || game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        if (request.Name != null) npc.Name = request.Name;
        if (request.Description != null) npc.Description = request.Description;
        if (request.Attributes != null) npc.Attributes = request.Attributes.GetValueOrDefault();
        if (request.Skills != null) npc.Skills = request.Skills.GetValueOrDefault();
        if (request.Inventory != null) npc.Inventory = request.Inventory.GetValueOrDefault();
        if (request.Spells != null) npc.Spells = request.Spells.GetValueOrDefault();
        if (request.PlotThreadId != null) npc.PlotThreadId = request.PlotThreadId;

        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new NPCUpdated(npc.GameId, npc.Id));

        return Ok(new { npc.Id, npc.Name, npc.Description });
    }

    [HttpDelete("npcs/{npcId}")]
    public async Task<IActionResult> DeleteNPC(Guid npcId)
    {
        var npc = await _context.NPCs.FindAsync(npcId);
        if (npc == null) return NotFound(new { error = "NPC not found." });

        var game = await _context.Games.FindAsync(npc.GameId);
        if (game == null || game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        _context.NPCs.Remove(npc);
        await _context.SaveChangesAsync();

        await _eventBus.PublishAsync(new NPCDeleted(npc.GameId, npc.Id));

        return Ok(new { message = "NPC deleted." });
    }

}
