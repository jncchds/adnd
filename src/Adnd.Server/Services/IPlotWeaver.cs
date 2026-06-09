using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// PlotWeaver — automatically generates, maintains, and evolves plot threads
/// based on game events. Fully automatic: no manual triggers needed.
/// </summary>
public interface IPlotWeaver
{
    /// <summary>
    /// Generate initial plot threads from game creation inputs.
    /// Called once when the game starts.
    /// </summary>
    Task<List<PlotThread>> GenerateInitialThreadsAsync(
        Guid gameId, string premise, string parameters, string systemId, Guid? presetId);

    /// <summary>
    /// Review all active plot threads and adapt them based on recent game events.
    /// Called automatically on significant events.
    /// </summary>
    Task<PlotReview> ReviewAndAdaptAsync(Guid gameId, string context, string trigger);

    /// <summary>
    /// Adjust momentum for a specific thread after an event.
    /// Lightweight — does not call the LLM.
    /// </summary>
    Task UpdateMomentumAsync(Guid gameId, Guid threadId, float delta, string reason);

    /// <summary>
    /// Get prioritized active threads for display.
    /// </summary>
    Task<List<PlotThread>> GetActiveThreadsAsync(Guid gameId);

    /// <summary>
    /// Get review history for a game.
    /// </summary>
    Task<List<PlotReview>> GetReviewHistoryAsync(Guid gameId, int limit = 20);

    /// <summary>
    /// Check if initial threads have been generated for a game.
    /// </summary>
    Task<bool> HasInitialThreadsAsync(Guid gameId);

    // ==================== Dynamic Generation ====================

    /// <summary>
    /// Generate new plot threads from recent game context.
    /// Called during reviews when the LLM detects story opportunities.
    /// </summary>
    Task<List<PlotThread>> GenerateNewThreadsAsync(Guid gameId, string context, string trigger);

    /// <summary>
    /// Spawn milestone events for threads whose momentum is high enough.
    /// Called when a thread's momentum crosses the milestone threshold.
    /// </summary>
    Task<List<MilestoneEvent>> SpawnMilestonesAsync(Guid gameId);

    /// <summary>
    /// Detect story opportunities from recent game events.
    /// Returns a list of suggested actions (new threads, milestones, adaptations).
    /// </summary>
    Task<List<StoryOpportunity>> DetectOpportunitiesAsync(Guid gameId, string context);
}
