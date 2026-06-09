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
    INotificationHandler<NPCCreated>,
    INotificationHandler<NPCDeleted>,
    INotificationHandler<NPCUpdated>,
    INotificationHandler<CharacterUpdated>,
    INotificationHandler<StorySwayed>,
    INotificationHandler<PlayerJoined>,
    INotificationHandler<MessageSent>,
    INotificationHandler<PlotThreadCreated>,
    INotificationHandler<PlotThreadUpdated>
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

        _logger.LogInformation("[PLOTWEAVER] GameStarted | GameId={GameId} | System={SystemId} | PlotSeed={PlotSeed}",
            notification.GameId, game.SystemId, game.PlotSeed ?? "(none)");

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

                _logger.LogInformation("[PLOTWEAVER] GeneratedInitialThreads | GameId={GameId} | Count={Count}",
                    notification.GameId, threads.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PLOTWEAVER] FailedToGenerateInitialThreads | GameId={GameId} | Error={Error}",
                    notification.GameId, ex.Message);
            }
        }
        else
        {
            _logger.LogInformation("[PLOTWEAVER] InitialThreadsExist | GameId={GameId} — skipping generation",
                notification.GameId);
        }
    }

    // ==================== Combat Events ====================

    public async Task Handle(CombatStarted notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLOTWEAVER] CombatStarted | GameId={GameId} | CombatId={CombatId}",
            notification.GameId, notification.GameId);

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

        _logger.LogInformation("[PLOTWEAVER] ThreatEscalated | GameId={GameId} | ThreadsAffected={Count}",
            notification.GameId, threads.Count);
    }

    public async Task Handle(CombatEnded notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLOTWEAVER] CombatEnded | GameId={GameId} | Result={Result}",
            notification.GameId, notification.Result ?? "(none)");

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
                    _logger.LogInformation("[PLOTWEAVER] ThreatResolved | GameId={GameId} | ThreadId={ThreadId} | MomentumBump=+5",
                        notification.GameId, thread.Id);
                }
            }
        }
    }

    // ==================== NPC Events ====================

    public async Task Handle(NPCDeleted notification, CancellationToken ct)
    {
        var npc = await _context.NPCs.FindAsync(notification.NPCId);
        if (npc == null) return;

        _logger.LogInformation("[PLOTWEAVER] NPCDeleted | GameId={GameId} | NPCId={NPCId} | Name={Name}",
            notification.GameId, notification.NPCId, npc.Name);

        // Find plot threads associated with this NPC
        var threads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        var affectedCount = 0;
        foreach (var thread in threads)
        {
            if (thread.Description.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) ||
                thread.NextMilestone?.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) == true)
            {
                await _plotWeaver.UpdateMomentumAsync(
                    notification.GameId, thread.Id, -2f, $"NPC '{npc.Name}' deleted");
                affectedCount++;
            }
        }

        _logger.LogInformation("[PLOTWEAVER] NPCDeletedMomentum | GameId={GameId} | NPC={Name} | ThreadsAffected={Count}",
            notification.GameId, npc.Name, affectedCount);

        // Trigger a full review since NPC death can ripple through the story
        try
        {
            await _plotWeaver.ReviewAndAdaptAsync(
                notification.GameId,
                $"NPC '{npc.Name}' was deleted. This may affect multiple plot threads.",
                "NPCDeleted");
            _logger.LogInformation("[PLOTWEAVER] NPCDeletedReview | GameId={GameId} | Trigger=NPCDeleted", notification.GameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PLOTWEAVER] NPCDeletedReviewFailed | GameId={GameId} | Error={Error}", notification.GameId, ex.Message);
        }
    }

    public async Task Handle(NPCUpdated notification, CancellationToken ct)
    {
        var npc = await _context.NPCs.FindAsync(notification.NPCId);
        if (npc == null) return;

        _logger.LogInformation("[PLOTWEAVER] NPCUpdated | GameId={GameId} | NPCId={NPCId} | Name={Name}",
            notification.GameId, notification.NPCId, npc.Name);

        var threads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        var affectedCount = 0;
        foreach (var thread in threads.Where(t =>
            t.Description.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) ||
            t.NextMilestone?.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) == true))
        {
            await _plotWeaver.UpdateMomentumAsync(
                notification.GameId, thread.Id, 1f, $"NPC '{npc.Name}' updated");
            affectedCount++;
        }

        _logger.LogInformation("[PLOTWEAVER] NPCUpdatedMomentum | GameId={GameId} | NPC={Name} | ThreadsAffected={Count}",
            notification.GameId, npc.Name, affectedCount);
    }

    // ==================== Character Events ====================

    public async Task Handle(CharacterUpdated notification, CancellationToken ct)
    {
        var character = await _context.Characters.FindAsync(notification.CharacterId);
        if (character == null) return;

        _logger.LogInformation("[PLOTWEAVER] CharacterUpdated | GameId={GameId} | CharacterId={CharacterId} | Name={Name}",
            notification.GameId, notification.CharacterId, character.Name);

        // Check if HP changed significantly
        var threads = await _context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        var affectedCount = 0;
        foreach (var thread in threads.Where(t => t.Category == PlotThreadCategory.Personal))
        {
            if (thread.Description.Contains(character.Name, StringComparison.OrdinalIgnoreCase))
            {
                await _plotWeaver.UpdateMomentumAsync(
                    notification.GameId, thread.Id, 1f, $"Character '{character.Name}' updated");
                affectedCount++;
            }
        }

        _logger.LogInformation("[PLOTWEAVER] CharacterUpdatedMomentum | GameId={GameId} | Character={Name} | ThreadsAffected={Count}",
            notification.GameId, character.Name, affectedCount);
    }

    // ==================== Story Sway ====================

    public async Task Handle(StorySwayed notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLOTWEAVER] StorySwayed | GameId={GameId} | CreatorId={CreatorId} | Direction={Direction}",
            notification.GameId, notification.CreatorId, notification.Direction);

        // Trigger a full review to adapt threads to the new direction
        try
        {
            await _plotWeaver.ReviewAndAdaptAsync(
                notification.GameId,
                $"Creator swayed the story: {notification.Direction}",
                "StorySwayed");
            _logger.LogInformation("[PLOTWEAVER] StorySwayReview | GameId={GameId} | Trigger=StorySwayed", notification.GameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PLOTWEAVER] StorySwayReviewFailed | GameId={GameId} | Error={Error}", notification.GameId, ex.Message);
        }
    }

    // ==================== Player Events ====================

    public async Task Handle(PlayerJoined notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLOTWEAVER] PlayerJoined | GameId={GameId} | PlayerId={PlayerId} | Character={Character}",
            notification.GameId, notification.PlayerId, notification.CharacterName);

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
            _logger.LogInformation("[PLOTWEAVER] PlayerJoinedNoPersonal | GameId={GameId} | Character={Character} — triggering review",
                notification.GameId, notification.CharacterName);
            try
            {
                await _plotWeaver.ReviewAndAdaptAsync(
                    notification.GameId,
                    $"New player '{notification.CharacterName}' joined. Consider creating personal storyline threads.",
                    "PlayerJoined");
                _logger.LogInformation("[PLOTWEAVER] PlayerJoinedReview | GameId={GameId} | Trigger=PlayerJoined", notification.GameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PLOTWEAVER] PlayerJoinedReviewFailed | GameId={GameId} | Error={Error}", notification.GameId, ex.Message);
            }
        }
        else
        {
            _logger.LogInformation("[PLOTWEAVER] PlayerJoinedHasPersonal | GameId={GameId} | PersonalThreads={Count} — skipping review",
                notification.GameId, personalThreads);
        }
    }

    // ==================== NPC Creation ====================

    public Task Handle(NPCCreated notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLOTWEAVER] NPCCreated | GameId={GameId} | NPCId={NPCId} | Name={Name}",
            notification.GameId, notification.NPCId, notification.Name);
        return Task.CompletedTask;
    }

    // ==================== Plot Thread Events ====================

    public Task Handle(PlotThreadCreated notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLOTWEAVER] PlotThreadCreated | GameId={GameId} | ThreadId={ThreadId} | Title={Title}",
            notification.GameId, notification.ThreadId, notification.Title);
        return Task.CompletedTask;
    }

    public Task Handle(PlotThreadUpdated notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLOTWEAVER] PlotThreadUpdated | GameId={GameId} | ThreadId={ThreadId}",
            notification.GameId, notification.ThreadId);
        return Task.CompletedTask;
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

            _logger.LogInformation("[PLOTWEAVER] PeriodicReview | GameId={GameId} | Trigger=MessageCount ({Count}/{Threshold})",
                notification.GameId, _messageCountSinceReview, ReviewThreshold);

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

                _logger.LogInformation("[PLOTWEAVER] OpportunitiesDetected | GameId={GameId} | Count={Count}",
                    notification.GameId, opportunities.Count);

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
                                _logger.LogInformation("[PLOTWEAVER] OpportunityNewThread | GameId={GameId} | Title={Title} | Category={Category}",
                                    notification.GameId, opportunity.NewThreadTitle, opportunity.NewThreadCategory);
                                break;

                            case OpportunityType.SpawnMilestone:
                                var milestones = await _plotWeaver.SpawnMilestonesAsync(
                                    notification.GameId);
                                if (milestones.Any())
                                {
                                    foreach (var m in milestones)
                                    {
                                        _logger.LogInformation("[PLOTWEAVER] MilestoneSpawned | GameId={GameId} | Title={Title} | Description={Description}",
                                            notification.GameId, m.Title, m.Description);
                                    }
                                }
                                break;

                            case OpportunityType.EscalateThreat:
                                if (opportunity.MomentumDelta.HasValue && opportunity.ThreadId != null)
                                {
                                    await _plotWeaver.UpdateMomentumAsync(
                                        notification.GameId, Guid.Parse(opportunity.ThreadId),
                                        opportunity.MomentumDelta.Value, opportunity.Title);
                                    _logger.LogInformation("[PLOTWEAVER] OpportunityEscalateThreat | GameId={GameId} | ThreadId={ThreadId} | Delta={Delta:F1}",
                                        notification.GameId, opportunity.ThreadId, opportunity.MomentumDelta.Value);
                                }
                                break;

                            case OpportunityType.AdaptThread:
                                _logger.LogInformation("[PLOTWEAVER] OpportunityAdaptThread | GameId={GameId} | Title={Title}",
                                    notification.GameId, opportunity.Title);
                                break;

                            case OpportunityType.MergeThreads:
                                _logger.LogInformation("[PLOTWEAVER] OpportunityMergeThreads | GameId={GameId} | Title={Title}",
                                    notification.GameId, opportunity.Title);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PLOTWEAVER] FailedToActOnOpportunity | GameId={GameId} | Opportunity={Opportunity} | Error={Error}",
                            notification.GameId, opportunity.Type, ex.Message);
                    }
                }

                // Step 3: Full review to adapt all threads
                await _plotWeaver.ReviewAndAdaptAsync(
                    notification.GameId, context, "PeriodicReview");
                _logger.LogInformation("[PLOTWEAVER] PeriodicReviewComplete | GameId={GameId}", notification.GameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PLOTWEAVER] PeriodicReviewFailed | GameId={GameId} | Error={Error}", notification.GameId, ex.Message);
            }
        }
    }
}
