using Pgvector;

namespace Adnd.Server.Models;

public class PlotThread : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlotThreadCategory Category { get; set; } = PlotThreadCategory.General;
    public PlotThreadStatus Status { get; set; } = PlotThreadStatus.Active;
    public float Momentum { get; set; }
    public float RelevanceScore { get; set; } = 1.0f;
    public string? NextMilestone { get; set; }
    public string? Foreshadowing { get; set; }
    public List<string> AdaptationHistory { get; set; } = [];
    public List<MilestoneEvent> MilestoneEvents { get; set; } = [];
    public bool IsDynamic { get; set; } = true;
    public string? KeyEventMessageIds { get; set; }

    /// <summary>
    /// When the thread entered Resolved/Abandoned. Archiving needs this: the cleanup pass
    /// computed a 30-day cutoff but had no timestamp to compare it against, so it
    /// soft-deleted every resolved thread on the next run regardless of age.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }
    public Vector? Embedding { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Game Game { get; set; } = null!;
}

public class MilestoneEvent
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? TriggeredAt { get; set; }
}
