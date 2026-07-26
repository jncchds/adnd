using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Authorize]
public class QuickWinsController(AppDbContext db) : ControllerBase
{
    // ── Session Notes ─────────────────────────────────────────────────────────

    [HttpGet("api/quickwins/notes")]
    public async Task<IActionResult> ListNotes([FromQuery] Guid gameId, CancellationToken ct)
    {
        var notes = await db.SessionNotes
            .Where(n => n.GameId == gameId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);
        return Ok(notes);
    }

    [HttpPost("api/quickwins/notes")]
    public async Task<IActionResult> CreateNote([FromBody] SessionNote note, CancellationToken ct)
    {
        note.Id = Guid.NewGuid();
        note.CreatedAt = DateTimeOffset.UtcNow;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        db.SessionNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return Ok(note);
    }

    [HttpPut("api/quickwins/notes/{id:guid}")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] SessionNote update, CancellationToken ct)
    {
        var note = await db.SessionNotes.FindAsync([id], ct);
        if (note == null) return NotFound();
        note.Title = update.Title;
        note.Content = update.Content;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(note);
    }

    [HttpDelete("api/quickwins/notes/{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct)
    {
        var note = await db.SessionNotes.FindAsync([id], ct);
        if (note == null) return NotFound();
        db.SessionNotes.Remove(note);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Prompt Templates ──────────────────────────────────────────────────────

    [HttpGet("api/quickwins/templates")]
    public async Task<IActionResult> ListPromptTemplates([FromQuery] Guid? gameId, CancellationToken ct)
    {
        var templates = await db.PromptTemplates
            .Where(t => t.GameId == gameId || t.IsDefault)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return Ok(templates);
    }

    [HttpPost("api/quickwins/templates")]
    public async Task<IActionResult> CreatePromptTemplate([FromBody] PromptTemplate template, CancellationToken ct)
    {
        template.Id = Guid.NewGuid();
        template.CreatedAt = DateTimeOffset.UtcNow;
        db.PromptTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpPut("api/quickwins/templates/{id:guid}")]
    public async Task<IActionResult> UpdatePromptTemplate(Guid id, [FromBody] PromptTemplate update, CancellationToken ct)
    {
        var template = await db.PromptTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();
        template.Name = update.Name;
        template.Type = update.Type;
        template.Content = update.Content;
        template.IsDefault = update.IsDefault;
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpDelete("api/quickwins/templates/{id:guid}")]
    public async Task<IActionResult> DeletePromptTemplate(Guid id, CancellationToken ct)
    {
        var template = await db.PromptTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();
        db.PromptTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Game Templates ────────────────────────────────────────────────────────

    [HttpGet("api/quickwins/gametemplates")]
    public async Task<IActionResult> ListGameTemplates(CancellationToken ct)
    {
        var templates = await db.GameTemplates
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);
        return Ok(templates);
    }

    [HttpPost("api/quickwins/gametemplates")]
    public async Task<IActionResult> CreateGameTemplate([FromBody] GameTemplate template, CancellationToken ct)
    {
        template.Id = Guid.NewGuid();
        template.CreatedAt = DateTimeOffset.UtcNow;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        db.GameTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpPut("api/quickwins/gametemplates/{id:guid}")]
    public async Task<IActionResult> UpdateGameTemplate(Guid id, [FromBody] GameTemplate update, CancellationToken ct)
    {
        var template = await db.GameTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();
        template.Name = update.Name;
        template.Description = update.Description;
        template.SystemId = update.SystemId;
        template.Language = update.Language;
        template.PlotSeed = update.PlotSeed;
        template.GameParameters = update.GameParameters;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpDelete("api/quickwins/gametemplates/{id:guid}")]
    public async Task<IActionResult> DeleteGameTemplate(Guid id, CancellationToken ct)
    {
        var template = await db.GameTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();
        db.GameTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
