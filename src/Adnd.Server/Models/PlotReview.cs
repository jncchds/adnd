namespace Adnd.Server.Models;

/// <summary>
/// Records a PlotWeaver review — a point-in-time evaluation of all plot threads
/// and the reasoning behind any adaptations.
/// </summary>
public class PlotReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Why this review was triggered (event name or "manual").
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// LLM-generated summary of the review.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// What changed and why.
    /// </summary>
    public List<ThreadUpdate> Updates { get; set; } = new();
}

public class ThreadUpdate
{
    public Guid ThreadId { get; set; }
    public string ThreadTitle { get; set; } = string.Empty;

    public float? OldMomentum { get; set; }
    public float? NewMomentum { get; set; }

    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }

    public string? OldDescription { get; set; }
    public string? NewDescription { get; set; }

    public string? OldMilestone { get; set; }
    public string? NewMilestone { get; set; }

    public string Reason { get; set; } = string.Empty;
}
