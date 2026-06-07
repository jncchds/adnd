namespace Adnd.Server.Models;

public class PlotThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PlotThreadCategory Category { get; set; } = PlotThreadCategory.General;
    public PlotThreadStatus Status { get; set; } = PlotThreadStatus.Active;

    // Momentum: -10 (abandoned) to +10 (urgent). Driven by game events.
    public float Momentum { get; set; } = 0f;

    // Relevance to current game state (computed by PlotWeaver)
    public float RelevanceScore { get; set; } = 0f;

    // Next expected milestone event in this thread
    public string? NextMilestone { get; set; }

    // Foreshadowing / planted hints for this thread
    public string? Foreshadowing { get; set; }

    // LLM adaptation log — each entry is a reason why the thread was updated
    public List<string> AdaptationHistory { get; set; } = new();

    // Milestone events — concrete events tied to this thread
    public List<MilestoneEvent> MilestoneEvents { get; set; } = new();

    // Whether this thread was generated dynamically (vs. initial)
    public bool IsDynamic { get; set; }

    public List<Guid> KeyEventMessageIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // PGVector embedding
    public float[]? Embedding { get; set; }
}

public enum PlotThreadStatus
{
    Active,
    Resolved,
    Abandoned
}

public enum PlotThreadCategory
{
    General,
    Faction,
    Mystery,
    Personal,
    Threat,
    WorldEvent,
    Relationship
}

/// <summary>
/// A concrete event tied to a plot thread.
/// </summary>
public class MilestoneEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Pending;
    public DateTime? TriggeredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum MilestoneStatus
{
    Pending,
    Triggered,
    Completed,
    Abandoned
}
