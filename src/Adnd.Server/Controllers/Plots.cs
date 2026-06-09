using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Plot Thread Management ====================

    [HttpGet("games/{gameId}/plot-threads")]
    public async Task<IActionResult> GetPlotThreads(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var threads = await _plotWeaver.GetActiveThreadsAsync(gameId);

        return Ok(threads.Select(t => new
        {
            t.Id,
            t.Title,
            t.Category,
            t.Description,
            t.Status,
            t.Momentum,
            t.RelevanceScore,
            t.NextMilestone,
            t.Foreshadowing,
            t.AdaptationHistory,
            t.CreatedAt,
            t.UpdatedAt
        }));
    }

    [HttpPost("games/{gameId}/plot-threads")]
    public async Task<IActionResult> CreatePlotThread(Guid gameId, [FromBody] CreatePlotThreadRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var thread = new PlotThread
        {
            GameId = gameId,
            Title = request.Title,
            Description = request.Description,
            Status = PlotThreadStatus.Active,
            KeyEventMessageIds = new List<Guid>(),
            CreatedAt = DateTime.UtcNow
        };

        _context.PlotThreads.Add(thread);
        await _context.SaveChangesAsync();

        return Ok(new { thread.Id, thread.Title, thread.Description, thread.Status });
    }

    [HttpPut("plot-threads/{threadId}")]
    public async Task<IActionResult> UpdatePlotThread(Guid threadId, [FromBody] UpdatePlotThreadRequest request)
    {
        var thread = await _context.PlotThreads.FindAsync(threadId);
        if (thread == null) return NotFound(new { error = "Plot thread not found." });

        var game = await _context.Games.FindAsync(thread.GameId);
        if (game == null || game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        if (request.Title != null) thread.Title = request.Title;
        if (request.Description != null) thread.Description = request.Description;
        if (request.Status != null) thread.Status = (PlotThreadStatus)request.Status;
        if (request.KeyEventMessageIds != null) thread.KeyEventMessageIds = request.KeyEventMessageIds;
        thread.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { thread.Id, thread.Title, thread.Description, thread.Status });
    }

    [HttpPost("plot-threads/{threadId}/add-event")]
    public async Task<IActionResult> AddKeyEvent(Guid threadId, [FromBody] AddKeyEventRequest request)
    {
        var thread = await _context.PlotThreads.FindAsync(threadId);
        if (thread == null) return NotFound(new { error = "Plot thread not found." });

        if (!thread.KeyEventMessageIds.Contains(request.MessageId))
        {
            thread.KeyEventMessageIds.Add(request.MessageId);
            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Event added to plot thread." });
    }

}
