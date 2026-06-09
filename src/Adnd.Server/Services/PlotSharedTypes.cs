using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// A story opportunity detected by PlotWeaver.
/// </summary>
public class StoryOpportunity
{
    public OpportunityType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public float? MomentumDelta { get; set; }
    public string? NewThreadCategory { get; set; }
    public string? NewThreadTitle { get; set; }
    public string? NewThreadDescription { get; set; }
    public string? NewMilestone { get; set; }
}

public enum OpportunityType
{
    NewThread,
    SpawnMilestone,
    AdaptThread,
    MergeThreads,
    EscalateThreat
}
