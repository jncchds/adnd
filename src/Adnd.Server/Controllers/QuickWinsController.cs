using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

/// <summary>
/// Session notes, prompt templates and game templates. Every endpoint here previously
/// looked its target up by primary key with no ownership check at all, and bound EF
/// entities straight from the request body.
/// </summary>
public class QuickWinsController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    // ── Session Notes (scoped to the game) ────────────────────────────────────

    [HttpGet("api/quickwins/notes")]
    public async Task<IActionResult> ListNotes([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (await RequireMember(gameId) is { } failure) return failure;

        var notes = await db.SessionNotes
            .AsNoTracking()
            .Where(n => n.GameId == gameId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);
        return Ok(notes);
    }

    [HttpPost("api/quickwins/notes")]
    public async Task<IActionResult> CreateNote([FromBody] CreateSessionNoteDto dto, CancellationToken ct)
    {
        if (await RequireMember(dto.GameId) is { } failure) return failure;

        var note = new SessionNote
        {
            GameId = dto.GameId,
            SessionId = dto.SessionId,
            Title = dto.Title,
            Content = dto.Content
        };
        db.SessionNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return Ok(note);
    }

    [HttpPut("api/quickwins/notes/{id:guid}")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateSessionNoteDto dto, CancellationToken ct)
    {
        var note = await db.SessionNotes.FindAsync([id], ct);
        if (note == null) return NotFound();
        if (await RequireMember(note.GameId) is { } failure) return failure;

        if (dto.Title is not null) note.Title = dto.Title;
        if (dto.Content is not null) note.Content = dto.Content;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(note);
    }

    [HttpDelete("api/quickwins/notes/{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct)
    {
        var note = await db.SessionNotes.FindAsync([id], ct);
        if (note == null) return NotFound();
        if (await RequireMember(note.GameId) is { } failure) return failure;

        db.SessionNotes.Remove(note);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Prompt Templates (game-scoped, plus read-only built-in defaults) ──────

    [HttpGet("api/quickwins/templates")]
    public async Task<IActionResult> ListPromptTemplates([FromQuery] Guid? gameId, CancellationToken ct)
    {
        if (gameId is { } id && await RequireMember(id) is { } failure) return failure;

        var templates = await db.PromptTemplates
            .AsNoTracking()
            .Where(t => (gameId != null && t.GameId == gameId) || t.IsDefault)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return Ok(templates);
    }

    [HttpPost("api/quickwins/templates")]
    public async Task<IActionResult> CreatePromptTemplate([FromBody] CreatePromptTemplateDto dto, CancellationToken ct)
    {
        // A template with no game is a built-in default; only game-scoped ones are user-creatable.
        if (dto.GameId is not { } gameId)
            return BadRequest(new { error = "GameId is required." });
        if (await RequireCreator(gameId) is { } failure) return failure;

        var template = new PromptTemplate
        {
            GameId = gameId,
            Name = dto.Name,
            Type = dto.Type,
            Content = dto.Content,
            IsDefault = false
        };
        db.PromptTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpPut("api/quickwins/templates/{id:guid}")]
    public async Task<IActionResult> UpdatePromptTemplate(Guid id, [FromBody] UpdatePromptTemplateDto dto, CancellationToken ct)
    {
        var template = await db.PromptTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();

        if (await RequireTemplateOwnerAsync(template) is { } failure) return failure;

        if (dto.Name is not null) template.Name = dto.Name;
        if (dto.Type is not null) template.Type = dto.Type;
        if (dto.Content is not null) template.Content = dto.Content;
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpDelete("api/quickwins/templates/{id:guid}")]
    public async Task<IActionResult> DeletePromptTemplate(Guid id, CancellationToken ct)
    {
        var template = await db.PromptTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();

        if (await RequireTemplateOwnerAsync(template) is { } failure) return failure;

        db.PromptTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult?> RequireTemplateOwnerAsync(PromptTemplate template)
    {
        // Built-in defaults belong to nobody and are not editable through the API.
        if (template.GameId is not { } gameId)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Built-in templates cannot be modified." });

        return await RequireCreator(gameId);
    }

    // ── Game Templates (owned by the user) ────────────────────────────────────

    [HttpGet("api/quickwins/gametemplates")]
    public async Task<IActionResult> ListGameTemplates(CancellationToken ct)
    {
        // This used to return every game template belonging to every user.
        var templates = await db.GameTemplates
            .AsNoTracking()
            .Where(t => t.UserId == CurrentUserId)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);
        return Ok(templates);
    }

    [HttpPost("api/quickwins/gametemplates")]
    public async Task<IActionResult> CreateGameTemplate([FromBody] CreateGameTemplateDto dto, CancellationToken ct)
    {
        var template = new GameTemplate
        {
            // Taken from the token; CreateGameTemplate never set it at all.
            UserId = CurrentUserId,
            Name = dto.Name,
            Description = dto.Description,
            SystemId = dto.SystemId,
            Language = dto.Language,
            PlotSeed = dto.PlotSeed,
            GameParameters = dto.GameParameters,
            LLMPresetId = dto.LLMPresetId
        };
        db.GameTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpPut("api/quickwins/gametemplates/{id:guid}")]
    public async Task<IActionResult> UpdateGameTemplate(Guid id, [FromBody] UpdateGameTemplateDto dto, CancellationToken ct)
    {
        var template = await db.GameTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();
        if (template.UserId != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "This template belongs to another user." });

        if (dto.Name is not null) template.Name = dto.Name;
        if (dto.Description is not null) template.Description = dto.Description;
        if (dto.SystemId is not null) template.SystemId = dto.SystemId;
        if (dto.Language is not null) template.Language = dto.Language;
        if (dto.PlotSeed is not null) template.PlotSeed = dto.PlotSeed;
        if (dto.GameParameters is not null) template.GameParameters = dto.GameParameters;
        if (dto.LLMPresetId.HasValue) template.LLMPresetId = dto.LLMPresetId;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    [HttpDelete("api/quickwins/gametemplates/{id:guid}")]
    public async Task<IActionResult> DeleteGameTemplate(Guid id, CancellationToken ct)
    {
        var template = await db.GameTemplates.FindAsync([id], ct);
        if (template == null) return NotFound();
        if (template.UserId != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "This template belongs to another user." });

        db.GameTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
