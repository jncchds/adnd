using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Services;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== PlotWeaver ====================

    [HttpPost("games/{gameId}/plot-weaver/review")]
    public async Task<IActionResult> TriggerReview(Guid gameId, [FromBody] string? context = null)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var reviewContext = context ?? "Manual review triggered by creator.";
        var review = await _plotWeaver.ReviewAndAdaptAsync(gameId, reviewContext, "ManualReview");

        return Ok(new
        {
            review.Id,
            review.Trigger,
            review.Summary,
            review.Updates,
            review.ReviewedAt
        });
    }

    [HttpGet("games/{gameId}/plot-weaver/reviews")]
    public async Task<IActionResult> GetReviewHistory(Guid gameId, [FromQuery] int limit = 20)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var reviews = await _plotWeaver.GetReviewHistoryAsync(gameId, limit);

        return Ok(reviews.Select(r => new
        {
            r.Id,
            r.Trigger,
            r.Summary,
            r.Updates,
            r.ReviewedAt
        }));
    }

    [HttpPost("games/{gameId}/plot-weaver/threads/{threadId}/momentum")]
    public async Task<IActionResult> AdjustMomentum(Guid gameId, Guid threadId, [FromBody] AdjustMomentumRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        await _plotWeaver.UpdateMomentumAsync(gameId, threadId, request.Delta, request.Reason);

        return Ok(new { message = "Momentum adjusted." });
    }

    [HttpPost("games/{gameId}/plot-weaver/opportunities")]
    public async Task<IActionResult> DetectOpportunities(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        // Gather recent context
        var recentMessages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();

        var context = string.Join("\n", recentMessages.Select(m =>
            $"[{m.CreatedAt:HH:mm}] {m.Player?.CharacterName ?? "System"}: {m.Content}"));

        var opportunities = await _plotWeaver.DetectOpportunitiesAsync(gameId, context);

        // Auto-apply non-complex opportunities
        foreach (var opp in opportunities)
        {
            try
            {
                switch (opp.Type)
                {
                    case OpportunityType.NewThread:
                        await _plotWeaver.GenerateNewThreadsAsync(gameId, context, "ManualOpportunity");
                        break;
                    case OpportunityType.SpawnMilestone:
                        var milestones = await _plotWeaver.SpawnMilestonesAsync(gameId);
                        foreach (var m in milestones)
                        {
                            _logger.LogInformation("Manual milestone triggered: {Title} in game {GameId}",
                                m.Title, gameId);
                        }
                        break;
                    case OpportunityType.EscalateThreat:
                        if (opp.MomentumDelta.HasValue && opp.ThreadId != null)
                        {
                            await _plotWeaver.UpdateMomentumAsync(gameId, Guid.Parse(opp.ThreadId),
                                opp.MomentumDelta.Value, opp.Title);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-apply opportunity in game {GameId}", gameId);
            }
        }

        return Ok(opportunities.Select(o => new
        {
            o.Type,
            o.Title,
            o.Description,
            o.ThreadId,
            o.MomentumDelta,
            o.NewThreadCategory,
            o.NewThreadTitle,
            o.NewThreadDescription,
            o.NewMilestone
        }));
    }

}
