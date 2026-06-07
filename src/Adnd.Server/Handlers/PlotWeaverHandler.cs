using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

/// <summary>
/// Handles game events by triggering PlotWeaver actions automatically.
/// No manual triggers — everything is event-driven.
/// </summary>
public class PlotWeaverHandler :
    INotificationHandler<GameStarted>,
    INotificationHandler<CombatEnded>,
    INotificationHandler<CombatStarted>,
    INotificationHandler<NPCDeleted>,
    INotificationHandler<NPCUpdated>,
    INotificationHandler<CharacterUpdated>,
    INotificationHandler<StorySwayed>,
    INotificationHandler<PlayerJoined>,
    INotificationHandler<MessageSent>
{
    private readonly IPlotWeaver _plotWeaver;
    private readonly ILogger<PlotWeaverHandler> _logger;
    private readonly AppDbContext _context;
    private int _messageCountSinceReview;
    private const int ReviewThreshold = 15; // Review every N in-game messages

    public PlotWeaverHandler(
        IPlotWeaver plotWeaver,
        ILogger<PlotWeaverHandler> logger,
        AppDbContext context)
    {
        _plotWeaver = plotWeaver;
        _logger = logger;
        _context = context;
        _messageCountSinceReview = 0;
    }

    // ==================== Game Lifecycle ====================

    public async Task Handle(GameStarted notification, CancellationToken ct)
    {
        var game = await _context.Games.FindAsync(notification.GameId);
        if (game == null) return;

        // Generate initial plot threads if not already done
        if (!await _plotWeaver.HasInitialThreadsAsync(notification.GameId))
        {
            try
            {
                var threads = await _plotWeaver.GenerateInitialThreadsAsync(
                    notification.GameId,
                    game.PlotSeed ?? "No premise provided.",
                    game.GameParameters ?? "Standard tone and difficulty.",
                    game.SystemId,
                    game.LLMPresetId);

                _logger.LogInformation("Generated {Count} initial plot threads for game {GameId}",
                    threads.Count, notification.GameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate initial plot threads for game {GameId}", notification.GameId);
            }
        }
    }

    // ==================== Combat Events ====================

    public async Task Handle(CombatStarted notification, CancellationToken ct)
    {
        // Escalate threat-related threads
        var threads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Category == PlotThreadCategory.Threat &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        foreach (var thread in threads)
        {
            await _plotWeaver.UpdateMomentumAsync(
                notification.GameId, thread.Id, 2f, "Combat started — threat escalation");
        }

        _logger.LogDebug("Escalated threat threads for combat in game {GameId}", notification.GameId);
    }

    public async Task Handle(CombatEnded notification, CancellationToken ct)
    {
        var game = await _context.Games.FindAsync(notification.GameId);
        if (game == null) return;

        // Check if the combat had a clear winner/loser
        if (notification.Result != null)
        {
            // Resolve threat threads that were the cause of combat
            var threatThreads = await _context.PlotThreads
                .Where(t => t.GameId == notification.GameId &&
                            t.Category == PlotThreadCategory.Threat &&
                            t.Status == PlotThreadStatus.Active)
                .ToListAsync(ct);

            foreach (var thread in threatThreads)
            {
                // If combat resolved the threat, mark thread as resolved
                if (notification.Result.Contains("victory", StringComparison.OrdinalIgnoreCase) ||
                    notification.Result.Contains("defeat", StringComparison.OrdinalIgnoreCase))
                {
                    await _plotWeaver.UpdateMomentumAsync(
                        notification.GameId, thread.Id, 5f, "Combat resolved — threat impact");
                }
            }
        }
    }

    // ==================== NPC Events ====================

    public async Task Handle(NPCDeleted notification, CancellationToken ct)
    {
        var npc = await _context.NPCs.FindAsync(notification.NPCId);
        if (npc == null) return;

        // Find plot threads associated with this NPC
        var threads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        foreach (var thread in threads)
        {
            if (thread.Description.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) ||
                thread.NextMilestone?.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) == true)
            {
                await _plotWeaver.UpdateMomentumAsync(
                    notification.GameId, thread.Id, -2f, $"NPC '{npc.Name}' deleted");
            }
        }

        // Trigger a full review since NPC death can ripple through the story
        try
        {
            await _plotWeaver.ReviewAndAdaptAsync(
                notification.GameId,
                $"NPC '{npc.Name}' was deleted. This may affect multiple plot threads.",
                "NPCDeleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to review plot threads after NPC deletion in game {GameId}", notification.GameId);
        }
    }

    public async Task Handle(NPCUpdated notification, CancellationToken ct)
    {
        // Light momentum bump for related threads
        var npc = await _context.NPCs.FindAsync(notification.NPCId);
        if (npc == null) return;

        var threads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        foreach (var thread in threads.Where(t =>
            t.Description.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) ||
            t.NextMilestone?.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) == true))
        {
            await _plotWeaver.UpdateMomentumAsync(
                notification.GameId, thread.Id, 1f, $"NPC '{npc.Name}' updated");
        }
    }

    // ==================== Character Events ====================

    public async Task Handle(CharacterUpdated notification, CancellationToken ct)
    {
        var character = await _context.Characters.FindAsync(notification.CharacterId);
        if (character == null) return;

        // Check if HP changed significantly
        var threads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        foreach (var thread in threads.Where(t => t.Category == PlotThreadCategory.Personal))
        {
            if (thread.Description.Contains(character.Name, StringComparison.OrdinalIgnoreCase))
            {
                await _plotWeaver.UpdateMomentumAsync(
                    notification.GameId, thread.Id, 1f, $"Character '{character.Name}' updated");
            }
        }
    }

    // ==================== Story Sway ====================

    public async Task Handle(StorySwayed notification, CancellationToken ct)
    {
        _logger.LogInformation("Creator swayed story in game {GameId}: {Direction}",
            notification.GameId, notification.Direction);

        // Trigger a full review to adapt threads to the new direction
        try
        {
            await _plotWeaver.ReviewAndAdaptAsync(
                notification.GameId,
                $"Creator swayed the story: {notification.Direction}",
                "StorySwayed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to review plot threads after sway in game {GameId}", notification.GameId);
        }
    }

    // ==================== Player Events ====================

    public async Task Handle(PlayerJoined notification, CancellationToken ct)
    {
        var game = await _context.Games.FindAsync(notification.GameId);
        if (game == null) return;

        // Check if any personal storyline threads exist
        var personalThreads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Category == PlotThreadCategory.Personal &&
                        t.Status == PlotThreadStatus.Active)
            .CountAsync(ct);

        // If no personal threads, trigger a review to potentially add one
        if (personalThreads == 0)
        {
            try
            {
                await _plotWeaver.ReviewAndAdaptAsync(
                    notification.GameId,
                    $"New player '{notification.CharacterName}' joined. Consider creating personal storyline threads.",
                    "PlayerJoined");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to review after player joined in game {GameId}", notification.GameId);
            }
        }
    }

    // ==================== Message Events (periodic review) ====================

    public async Task Handle(MessageSent notification, CancellationToken ct)
    {
        // Only process in-game messages (not OOC or system)
        if (notification.Type != Adnd.Server.Events.MessageType.InGamePublic &&
            notification.Type != Adnd.Server.Events.MessageType.InGameWhisper)
            return;

        _messageCountSinceReview++;

        // Periodic review every N messages
        if (_messageCountSinceReview >= ReviewThreshold)
        {
            _messageCountSinceReview = 0;

            try
            {
                // Get recent context for the review
                var recentMessages = await _context.Messages
                    .Where(m => m.Session!.GameId == notification.GameId &&
                                (m.Type == Adnd.Server.Models.MessageType.InGamePublic || m.Type == Adnd.Server.Models.MessageType.InGameWhisper))
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(20)
                    .ToListAsync(ct);

                var context = string.Join("\n", recentMessages.Select(m =>
                    $"[{m.CreatedAt:HH:mm}] {m.Player?.CharacterName ?? "System"}: {m.Content}"));

                // Step 1: Detect story opportunities (new threads, milestones, etc.)
                var opportunities = await _plotWeaver.DetectOpportunitiesAsync(
                    notification.GameId, context);

                // Step 2: Act on opportunities
                foreach (var opportunity in opportunities)
                {
                    try
                    {
                        switch (opportunity.Type)
                        {
                            case OpportunityType.NewThread:
                                await _plotWeaver.GenerateNewThreadsAsync(
                                    notification.GameId, context, "OpportunityDetection");
                                break;

                            case OpportunityType.SpawnMilestone:
                                var milestones = await _plotWeaver.SpawnMilestonesAsync(
                                    notification.GameId);
                                if (milestones.Any())
                                {
                                    foreach (var m in milestones)
                                    {
                                        _logger.LogInformation("Milestone triggered: {Title} in game {GameId}",
                                            m.Title, notification.GameId);
                                    }
                                }
                                break;

                            case OpportunityType.EscalateThreat:
                                if (opportunity.MomentumDelta.HasValue && opportunity.ThreadId != null)
                                {
                                    await _plotWeaver.UpdateMomentumAsync(
                                        notification.GameId, Guid.Parse(opportunity.ThreadId),
                                        opportunity.MomentumDelta.Value, opportunity.Title);
                                }
                                break;

                            case OpportunityType.AdaptThread:
                                // Will be handled by the review below
                                break;

                            case OpportunityType.MergeThreads:
                                // Log for now — merging is complex
                                _logger.LogInformation("Thread merge opportunity: {Title} in game {GameId}",
                                    opportunity.Title, notification.GameId);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to act on opportunity '{Opportunity}' in game {GameId}",
                            opportunity.Type, notification.GameId);
                    }
                }

                // Step 3: Full review to adapt all threads
                await _plotWeaver.ReviewAndAdaptAsync(
                    notification.GameId, context, "PeriodicReview");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform periodic plot review in game {GameId}", notification.GameId);
            }
        }
    }
}
