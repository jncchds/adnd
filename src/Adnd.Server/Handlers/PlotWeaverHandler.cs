using System.Collections.Concurrent;
using System.Text.Json;
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
    IEventHandler<GameStarted>,
    IEventHandler<CombatEnded>,
    IEventHandler<CombatStarted>,
    IEventHandler<NPCCreated>,
    IEventHandler<NPCDeleted>,
    IEventHandler<NPCUpdated>,
    IEventHandler<CharacterUpdated>,
    IEventHandler<StorySwayed>,
    IEventHandler<PlayerJoined>,
    IEventHandler<MessageSent>,
    IEventHandler<PlotThreadCreated>,
    IEventHandler<PlotThreadUpdated>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlotWeaverHandler> _logger;
    // Per-game message counter — prevents cross-game review threshold pollution
    private readonly ConcurrentDictionary<Guid, int> _messageCountsByGame = new();
    private const int ReviewThreshold = 15;

    public PlotWeaverHandler(IServiceScopeFactory scopeFactory, ILogger<PlotWeaverHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ==================== Game Lifecycle ====================

    public async Task HandleAsync(GameStarted notification, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();
        var agentBus = scope.ServiceProvider.GetRequiredService<IAgentBus>();

        var game = await context.Games.FindAsync(notification.GameId);
        if (game == null) return;

        _logger.LogInformation("[PLOTWEAVER] GameStarted | GameId={GameId} | System={SystemId} | PlotSeed={PlotSeed}",
            notification.GameId, game.SystemId, game.PlotSeed ?? "(none)");

        if (!await plotWeaver.HasInitialThreadsAsync(notification.GameId))
        {
            try
            {
                var call = new AgentCall
                {
                    GameId = notification.GameId,
                    FromAgent = AgentType.System,
                    ToAgent = AgentType.GM,
                    Action = AgentAction.GenerateInitialThreads,
                    Input = JsonSerializer.Serialize(new GMDispatchOptions
                    {
                        SystemPrompt = $"You are the Game Master for a TTRPG session. " +
                            $"Generate initial plot threads for this game. " +
                            $"Plot seed: {game.PlotSeed ?? "No premise provided."}. " +
                            $"Game parameters: {game.GameParameters ?? "Standard tone and difficulty."}. " +
                            $"Game system: {game.SystemId}. " +
                            $"Respond with a JSON array of plot threads. Each thread should have: " +
                            $"title (string), category (Personal, Threat, Faction, Mystery, or Adventure), " +
                            $"description (string), nextMilestone (string), foreshadowing (string). " +
                            $"Generate 2-4 threads appropriate for the premise.",
                        UserPrompt = "Generate initial plot threads for this game."
                    }),
                    Status = AgentCallStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var queuedCall = await agentBus.SendCallAsync(call);

                _logger.LogInformation("[PLOTWEAVER] QueuedInitialThreadGeneration | GameId={GameId} | CallId={CallId}",
                    notification.GameId, queuedCall.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PLOTWEAVER] FailedToQueueInitialThreads | GameId={GameId} | Error={Error}",
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

    public async Task HandleAsync(CombatStarted notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLOTWEAVER] CombatStarted | GameId={GameId} | CombatId={CombatId}",
            notification.GameId, notification.GameId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        var threads = await context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Category == PlotThreadCategory.Threat &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        foreach (var thread in threads)
        {
            await plotWeaver.UpdateMomentumAsync(
                notification.GameId, thread.Id, 2f, "Combat started — threat escalation");
        }

        _logger.LogInformation("[PLOTWEAVER] ThreatEscalated | GameId={GameId} | ThreadsAffected={Count}",
            notification.GameId, threads.Count);
    }

    public async Task HandleAsync(CombatEnded notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLOTWEAVER] CombatEnded | GameId={GameId} | Result={Result}",
            notification.GameId, notification.Result ?? "(none)");

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        var game = await context.Games.FindAsync(notification.GameId);
        if (game == null) return;

        if (notification.Result != null)
        {
            var threatThreads = await context.PlotThreads
                .Where(t => t.GameId == notification.GameId &&
                            t.Category == PlotThreadCategory.Threat &&
                            t.Status == PlotThreadStatus.Active)
                .ToListAsync(ct);

            foreach (var thread in threatThreads)
            {
                if (notification.Result.Contains("victory", StringComparison.OrdinalIgnoreCase) ||
                    notification.Result.Contains("defeat", StringComparison.OrdinalIgnoreCase))
                {
                    await plotWeaver.UpdateMomentumAsync(
                        notification.GameId, thread.Id, 5f, "Combat resolved — threat impact");
                    _logger.LogInformation("[PLOTWEAVER] ThreatResolved | GameId={GameId} | ThreadId={ThreadId} | MomentumBump=+5",
                        notification.GameId, thread.Id);
                }
            }
        }
    }

    // ==================== NPC Events ====================

    public async Task HandleAsync(NPCDeleted notification, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        var npc = await context.NPCs.FindAsync(notification.NPCId);
        if (npc == null) return;

        _logger.LogInformation("[PLOTWEAVER] NPCDeleted | GameId={GameId} | NPCId={NPCId} | Name={Name}",
            notification.GameId, notification.NPCId, npc.Name);

        var threads = await context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        var affectedCount = 0;
        foreach (var thread in threads)
        {
            if (thread.Description.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) ||
                thread.NextMilestone?.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) == true)
            {
                await plotWeaver.UpdateMomentumAsync(
                    notification.GameId, thread.Id, -2f, $"NPC '{npc.Name}' deleted");
                affectedCount++;
            }
        }

        _logger.LogInformation("[PLOTWEAVER] NPCDeletedMomentum | GameId={GameId} | NPC={Name} | ThreadsAffected={Count}",
            notification.GameId, npc.Name, affectedCount);

        try
        {
            await plotWeaver.ReviewAndAdaptAsync(
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

    public async Task HandleAsync(NPCUpdated notification, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        var npc = await context.NPCs.FindAsync(notification.NPCId);
        if (npc == null) return;

        _logger.LogInformation("[PLOTWEAVER] NPCUpdated | GameId={GameId} | NPCId={NPCId} | Name={Name}",
            notification.GameId, notification.NPCId, npc.Name);

        var threads = await context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        var affectedCount = 0;
        foreach (var thread in threads.Where(t =>
            t.Description.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) ||
            t.NextMilestone?.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) == true))
        {
            await plotWeaver.UpdateMomentumAsync(
                notification.GameId, thread.Id, 1f, $"NPC '{npc.Name}' updated");
            affectedCount++;
        }

        _logger.LogInformation("[PLOTWEAVER] NPCUpdatedMomentum | GameId={GameId} | NPC={Name} | ThreadsAffected={Count}",
            notification.GameId, npc.Name, affectedCount);
    }

    // ==================== Character Events ====================

    public async Task HandleAsync(CharacterUpdated notification, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        var character = await context.Characters.FindAsync(notification.CharacterId);
        if (character == null) return;

        _logger.LogInformation("[PLOTWEAVER] CharacterUpdated | GameId={GameId} | CharacterId={CharacterId} | Name={Name}",
            notification.GameId, notification.CharacterId, character.Name);

        var threads = await context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Status == PlotThreadStatus.Active)
            .ToListAsync(ct);

        var affectedCount = 0;
        foreach (var thread in threads.Where(t => t.Category == PlotThreadCategory.Personal))
        {
            if (thread.Description.Contains(character.Name, StringComparison.OrdinalIgnoreCase))
            {
                await plotWeaver.UpdateMomentumAsync(
                    notification.GameId, thread.Id, 1f, $"Character '{character.Name}' updated");
                affectedCount++;
            }
        }

        _logger.LogInformation("[PLOTWEAVER] CharacterUpdatedMomentum | GameId={GameId} | Character={Name} | ThreadsAffected={Count}",
            notification.GameId, character.Name, affectedCount);
    }

    // ==================== Story Sway ====================

    public async Task HandleAsync(StorySwayed notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLOTWEAVER] StorySwayed | GameId={GameId} | CreatorId={CreatorId} | Direction={Direction}",
            notification.GameId, notification.CreatorId, notification.Direction);

        using var scope = _scopeFactory.CreateScope();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        try
        {
            await plotWeaver.ReviewAndAdaptAsync(
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

    public async Task HandleAsync(PlayerJoined notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLOTWEAVER] PlayerJoined | GameId={GameId} | PlayerId={PlayerId} | Character={Character}",
            notification.GameId, notification.PlayerId, notification.CharacterName);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        var game = await context.Games.FindAsync(notification.GameId);
        if (game == null) return;

        var personalThreads = await context.PlotThreads
            .Where(t => t.GameId == notification.GameId &&
                        t.Category == PlotThreadCategory.Personal &&
                        t.Status == PlotThreadStatus.Active)
            .CountAsync(ct);

        if (personalThreads == 0)
        {
            _logger.LogInformation("[PLOTWEAVER] PlayerJoinedNoPersonal | GameId={GameId} | Character={Character} — triggering review",
                notification.GameId, notification.CharacterName);
            try
            {
                await plotWeaver.ReviewAndAdaptAsync(
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

    public Task HandleAsync(NPCCreated notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLOTWEAVER] NPCCreated | GameId={GameId} | NPCId={NPCId} | Name={Name}",
            notification.GameId, notification.NPCId, notification.Name);
        return Task.CompletedTask;
    }

    // ==================== Plot Thread Events ====================

    public Task HandleAsync(PlotThreadCreated notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLOTWEAVER] PlotThreadCreated | GameId={GameId} | ThreadId={ThreadId} | Title={Title}",
            notification.GameId, notification.ThreadId, notification.Title);
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlotThreadUpdated notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLOTWEAVER] PlotThreadUpdated | GameId={GameId} | ThreadId={ThreadId}",
            notification.GameId, notification.ThreadId);
        return Task.CompletedTask;
    }

    // ==================== Message Events (periodic review) ====================

    public async Task HandleAsync(MessageSent notification, CancellationToken ct = default)
    {
        if (notification.Type != Adnd.Server.Events.MessageType.InGamePublic &&
            notification.Type != Adnd.Server.Events.MessageType.InGameWhisper)
            return;

        var count = _messageCountsByGame.AddOrUpdate(
            notification.GameId,
            _ => 1,
            (_, existing) => existing + 1);

        if (count < ReviewThreshold) return;

        _messageCountsByGame[notification.GameId] = 0;

        _logger.LogInformation("[PLOTWEAVER] PeriodicReview | GameId={GameId} | Trigger=MessageCount ({Count}/{Threshold}) | MessageLen={MessageLen}",
            notification.GameId, count, ReviewThreshold, notification.Content?.Length ?? 0);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plotWeaver = scope.ServiceProvider.GetRequiredService<IPlotWeaver>();

        try
        {
            var recentMessages = await context.Messages
                .Where(m => m.Session!.GameId == notification.GameId &&
                            (m.Type == Adnd.Server.Models.MessageType.InGamePublic || m.Type == Adnd.Server.Models.MessageType.InGameWhisper))
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            var messageContext = string.Join("\n", recentMessages.Select(m =>
                $"[{m.CreatedAt:HH:mm}] {m.Player?.CharacterName ?? "System"}: {m.Content}"));

            var opportunities = await plotWeaver.DetectOpportunitiesAsync(notification.GameId, messageContext);

            _logger.LogInformation("[PLOTWEAVER] OpportunitiesDetected | GameId={GameId} | Count={Count}",
                notification.GameId, opportunities.Count);

            foreach (var opportunity in opportunities)
            {
                try
                {
                    switch (opportunity.Type)
                    {
                        case OpportunityType.NewThread:
                            await plotWeaver.GenerateNewThreadsAsync(notification.GameId, messageContext, "OpportunityDetection");
                            _logger.LogInformation("[PLOTWEAVER] OpportunityNewThread | GameId={GameId} | Title={Title} | Category={Category}",
                                notification.GameId, opportunity.NewThreadTitle, opportunity.NewThreadCategory);
                            break;

                        case OpportunityType.SpawnMilestone:
                            var milestones = await plotWeaver.SpawnMilestonesAsync(notification.GameId);
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
                                await plotWeaver.UpdateMomentumAsync(
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

            await plotWeaver.ReviewAndAdaptAsync(notification.GameId, messageContext, "PeriodicReview");
            _logger.LogInformation("[PLOTWEAVER] PeriodicReviewComplete | GameId={GameId}", notification.GameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PLOTWEAVER] PeriodicReviewFailed | GameId={GameId} | Error={Error}", notification.GameId, ex.Message);
        }
    }
}
