using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[Route("api/[controller]")]
public class PlotsController(
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid gameId, CancellationToken ct)
    {
        if (await RequireMember(gameId) is { } failure) return failure;
        return Ok(await db.PlotThreads.AsNoTracking().Where(p => p.GameId == gameId).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var thread = await db.PlotThreads.FindAsync(id);
        if (thread == null) return NotFound();
        if (await RequireMember(thread.GameId) is { } failure) return failure;
        return Ok(thread);
    }

    // Plot threads are GM-authored. Binding PlotThread directly also let the caller pick
    // Id, GameId and IsDeleted, so these take DTOs.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlotThreadDto dto)
    {
        if (await RequireCreator(dto.GameId) is { } failure) return failure;

        var thread = new PlotThread
        {
            GameId = dto.GameId,
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            Status = dto.Status,
            Momentum = dto.Momentum,
            NextMilestone = dto.NextMilestone,
            Foreshadowing = dto.Foreshadowing,
            IsDynamic = dto.IsDynamic
        };

        db.PlotThreads.Add(thread);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = thread.Id }, thread);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlotThreadDto dto)
    {
        var thread = await db.PlotThreads.FindAsync(id);
        if (thread == null) return NotFound();
        if (await RequireCreator(thread.GameId) is { } failure) return failure;

        if (dto.Title is not null) thread.Title = dto.Title;
        if (dto.Description is not null) thread.Description = dto.Description;
        if (dto.Category.HasValue) thread.Category = dto.Category.Value;
        if (dto.Status.HasValue && dto.Status.Value != thread.Status)
        {
            thread.Status = dto.Status.Value;
            // Stamp (or clear) the archiving clock as the thread enters or leaves a terminal state.
            thread.ResolvedAt = thread.Status is PlotThreadStatus.Resolved or PlotThreadStatus.Abandoned
                ? DateTimeOffset.UtcNow
                : null;
        }
        if (dto.Momentum.HasValue) thread.Momentum = dto.Momentum.Value;
        if (dto.NextMilestone is not null) thread.NextMilestone = dto.NextMilestone;
        if (dto.Foreshadowing is not null) thread.Foreshadowing = dto.Foreshadowing;
        if (dto.IsDynamic.HasValue) thread.IsDynamic = dto.IsDynamic.Value;

        await db.SaveChangesAsync();
        return Ok(thread);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var thread = await db.PlotThreads.FindAsync(id);
        if (thread == null) return NotFound();
        if (await RequireCreator(thread.GameId) is { } failure) return failure;

        thread.IsDeleted = true;
        thread.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
