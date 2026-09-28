namespace Adnd.Server.Models;

/// <summary>
/// The game creator's one lever on a story the LLM runs: a direction for the narration
/// ("bring the cult back into it", "the party has had enough of the sewers"). It is put in
/// front of the narrator — and the plot weaver — for a number of GM turns, or until withdrawn,
/// and never shown to players.
/// </summary>
public class StoryNudge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>Narrator turns it still applies to; null keeps it until withdrawn.</summary>
    public int? TurnsRemaining { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when it runs out of turns or is withdrawn; an expired nudge stays as history.</summary>
    public DateTimeOffset? ExpiredAt { get; set; }

    public Game Game { get; set; } = null!;
}
