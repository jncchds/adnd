using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

/// <summary>
/// Admin endpoints — creator-only access for game management and monitoring.
/// Full implementation deferred to later phases; currently returns summaries and lists.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController(
    AppDbContext db,
    IUserIdProvider userIdProvider,
    ISystemRegistry systemRegistry) : ControllerBase
{
    private Guid CurrentUserId => userIdProvider.GetUserId();

    // ── Game / Character / NPC summaries ─────────────────────────────────────

    [HttpGet("npcs")]
    public async Task<IActionResult> NpcsSummary([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var npcs = await db.NPCs.Where(n => n.GameId == gameId).ToListAsync(ct);
        return Ok(new { count = npcs.Count, items = npcs });
    }

    [HttpGet("plotthreads")]
    public async Task<IActionResult> PlotThreadsSummary([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var threads = await db.PlotThreads.Where(p => p.GameId == gameId).ToListAsync(ct);
        return Ok(new { count = threads.Count, items = threads });
    }

    [HttpGet("characters")]
    public async Task<IActionResult> CharactersSummary([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var characters = await db.Characters
            .Join(db.Players.Where(p => p.GameId == gameId),
                  c => c.PlayerId,
                  p => p.Id,
                  (c, p) => c)
            .ToListAsync(ct);
        return Ok(new { count = characters.Count, items = characters });
    }

    // ── LLM Presets ───────────────────────────────────────────────────────────

    [HttpGet("llmpresets")]
    public async Task<IActionResult> LlmPresets(CancellationToken ct)
    {
        var presets = await db.LLMPresets
            .Where(p => p.UserId == CurrentUserId)
            .ToListAsync(ct);
        return Ok(presets);
    }

    // ── Systems ───────────────────────────────────────────────────────────────

    [HttpGet("systems")]
    public IActionResult Systems()
    {
        var systems = systemRegistry.GetBuiltInSystems();
        return Ok(systems);
    }

    // ── Agent monitoring ──────────────────────────────────────────────────────

    [HttpGet("agentcalls")]
    public async Task<IActionResult> AgentCallsList([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var calls = await db.AgentCalls
            .Where(a => a.GameId == gameId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return Ok(calls);
    }

    [HttpGet("whispers")]
    public async Task<IActionResult> Whispers([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var whispers = await db.Whispers
            .Join(db.GameSessions.Where(s => s.GameId == gameId),
                  w => w.SessionId,
                  s => s.Id,
                  (w, s) => w)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
        return Ok(whispers);
    }

    [HttpGet("toolcalls")]
    public async Task<IActionResult> ToolCalls([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var toolCalls = await db.GMToolCalls
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.StartedAt)
            .ToListAsync(ct);
        return Ok(toolCalls);
    }

    // ── Dice / Combat ─────────────────────────────────────────────────────────

    [HttpGet("dicehistory")]
    public async Task<IActionResult> DiceHistory([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var rolls = await db.Messages
            .Join(db.GameSessions.Where(s => s.GameId == gameId),
                  m => m.SessionId,
                  s => s.Id,
                  (m, s) => m)
            .Where(m => m.Type == "DiceRoll")
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
        return Ok(rolls);
    }

    [HttpGet("combats")]
    public async Task<IActionResult> CombatList([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var combats = await db.Combats
            .Where(c => c.GameId == gameId)
            .Include(c => c.Participants)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return Ok(combats);
    }

    // ── Spells / Session Notes / Messages / Templates ─────────────────────────

    [HttpGet("spells")]
    public async Task<IActionResult> SpellsSummary([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var characters = await db.Characters
            .Join(db.Players.Where(p => p.GameId == gameId),
                  c => c.PlayerId,
                  p => p.Id,
                  (c, p) => new { c.Id, c.Name, c.Spells })
            .ToListAsync(ct);
        return Ok(characters);
    }

    [HttpGet("sessionnotes")]
    public async Task<IActionResult> SessionNotes([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var notes = await db.SessionNotes
            .Where(n => n.GameId == gameId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);
        return Ok(notes);
    }

    [HttpGet("messages")]
    public async Task<IActionResult> Messages([FromQuery] Guid gameId, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!await IsCreator(gameId)) return Forbid();
        var msgs = await db.Messages
            .Join(db.GameSessions.Where(s => s.GameId == gameId),
                  m => m.SessionId,
                  s => s.Id,
                  (m, s) => m)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
        return Ok(msgs);
    }

    [HttpGet("prompttemplates")]
    public async Task<IActionResult> PromptTemplates([FromQuery] Guid? gameId, CancellationToken ct)
    {
        var templates = await db.PromptTemplates
            .Where(t => t.GameId == gameId || t.IsDefault)
            .ToListAsync(ct);
        return Ok(templates);
    }

    [HttpGet("gametemplates")]
    public async Task<IActionResult> GameTemplates(CancellationToken ct)
    {
        var templates = await db.GameTemplates
            .Where(t => t.UserId == CurrentUserId)
            .ToListAsync(ct);
        return Ok(templates);
    }

    [HttpGet("ragcontext")]
    public IActionResult RagContext([FromQuery] Guid gameId)
    {
        // RAG context retrieval — Phase 7 implementation
        return Ok(new { message = "RAG context endpoint — Phase 7", gameId });
    }

    [HttpGet("gmstatus")]
    public async Task<IActionResult> GmAgentStatus([FromQuery] Guid gameId, CancellationToken ct)
    {
        var game = await db.Games.FindAsync([gameId], ct);
        if (game == null) return NotFound();
        if (game.CreatorId != CurrentUserId) return Forbid();
        return Ok(new { game.GMStatus, game.LastGMAction, game.LastGMActionAt });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> IsCreator(Guid gameId)
    {
        var game = await db.Games.FindAsync(gameId);
        return game != null && game.CreatorId == CurrentUserId;
    }
}
