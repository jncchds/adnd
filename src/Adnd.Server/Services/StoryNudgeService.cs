using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public record StoryNudgeDto(Guid Id, string Text, int? TurnsRemaining, DateTimeOffset CreatedAt, DateTimeOffset? ExpiredAt);

/// <summary>
/// Nudges are how the game's creator steers a story the LLM runs. They replace "sway", whose
/// direction text was never read by anything: it only kicked off a plot review, so the creator
/// typed a direction and the narrator never saw a word of it.
/// </summary>
public interface IStoryNudgeService
{
    Task<List<StoryNudgeDto>> ListAsync(Guid gameId, CancellationToken ct = default);
    Task<StoryNudgeDto> CreateAsync(Guid gameId, Guid authorUserId, string text, int? turns, CancellationToken ct = default);

    /// <summary>False when the nudge isn't in this game or has already expired.</summary>
    Task<bool> WithdrawAsync(Guid gameId, Guid nudgeId, CancellationToken ct = default);

    /// <summary>Active nudges, without spending a turn — for the plot weaver.</summary>
    Task<List<string>> ActiveAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>Active nudges for one narrator turn, spending that turn from each limited one.</summary>
    Task<List<string>> TakeForNarrationAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>The prompt block for a list of nudges, or null for none.</summary>
    string? Describe(IReadOnlyList<string> nudges);
}

public class StoryNudgeService(AppDbContext db, IEventBus eventBus) : IStoryNudgeService
{
    public const int MaxTurns = 50;

    public async Task<List<StoryNudgeDto>> ListAsync(Guid gameId, CancellationToken ct = default)
        => await db.StoryNudges
            .Where(n => n.GameId == gameId)
            .OrderBy(n => n.ExpiredAt != null)
            .ThenByDescending(n => n.CreatedAt)
            .Take(30)
            .Select(n => new StoryNudgeDto(n.Id, n.Text, n.TurnsRemaining, n.CreatedAt, n.ExpiredAt))
            .ToListAsync(ct);

    public async Task<StoryNudgeDto> CreateAsync(Guid gameId, Guid authorUserId, string text, int? turns, CancellationToken ct = default)
    {
        var nudge = new StoryNudge
        {
            GameId = gameId,
            AuthorUserId = authorUserId,
            Text = text.Trim(),
            TurnsRemaining = turns is { } t ? Math.Clamp(t, 1, MaxTurns) : null,
        };
        db.StoryNudges.Add(nudge);
        await db.SaveChangesAsync(ct);

        // The plot weaver re-plans threads with the new direction in view, as sway once
        // promised to — the narrator picks it up on its next turn regardless.
        await eventBus.PublishAsync(new StoryNudged(gameId, nudge.Id), ct);
        return ToDto(nudge);
    }

    public async Task<bool> WithdrawAsync(Guid gameId, Guid nudgeId, CancellationToken ct = default)
    {
        var nudge = await db.StoryNudges.FirstOrDefaultAsync(n => n.Id == nudgeId && n.GameId == gameId && n.ExpiredAt == null, ct);
        if (nudge is null) return false;
        nudge.ExpiredAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<string>> ActiveAsync(Guid gameId, CancellationToken ct = default)
        => await db.StoryNudges
            .Where(n => n.GameId == gameId && n.ExpiredAt == null)
            .OrderBy(n => n.CreatedAt)
            .Select(n => n.Text)
            .ToListAsync(ct);

    public async Task<List<string>> TakeForNarrationAsync(Guid gameId, CancellationToken ct = default)
    {
        var active = await db.StoryNudges
            .Where(n => n.GameId == gameId && n.ExpiredAt == null)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

        foreach (var n in active.Where(n => n.TurnsRemaining is not null))
        {
            n.TurnsRemaining--;
            if (n.TurnsRemaining <= 0) n.ExpiredAt = DateTimeOffset.UtcNow;
        }
        if (active.Count > 0) await db.SaveChangesAsync(ct);

        return active.Select(n => n.Text).ToList();
    }

    public string? Describe(IReadOnlyList<string> nudges)
    {
        if (nudges.Count == 0) return null;
        var lines = string.Join("\n", nudges.Select(n => $"- {n}"));
        return "=== DIRECTION FROM THE STORYTELLER ===\n"
            + "The person who set up this campaign wants the story to move this way. Steer toward it through the world — events, NPCs, opportunities, consequences — over the coming turns, without forcing it into a moment where it doesn't fit and without ever taking a choice away from the players. Never mention or quote this direction; the players must not learn it exists.\n"
            + lines;
    }

    private static StoryNudgeDto ToDto(StoryNudge n) => new(n.Id, n.Text, n.TurnsRemaining, n.CreatedAt, n.ExpiredAt);
}
