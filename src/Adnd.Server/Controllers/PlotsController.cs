using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlotsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid gameId)
        => Ok(await db.PlotThreads.Where(p => p.GameId == gameId).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var thread = await db.PlotThreads.FindAsync(id);
        return thread == null ? NotFound() : Ok(thread);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlotThread thread)
    {
        db.PlotThreads.Add(thread);
        await db.SaveChangesAsync();
        return Ok(thread);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PlotThread update)
    {
        var thread = await db.PlotThreads.FindAsync(id);
        if (thread == null) return NotFound();
        thread.Title = update.Title;
        thread.Description = update.Description;
        thread.Status = update.Status;
        thread.Momentum = update.Momentum;
        await db.SaveChangesAsync();
        return Ok(thread);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var thread = await db.PlotThreads.FindAsync(id);
        if (thread == null) return NotFound();
        thread.IsDeleted = true;
        thread.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
