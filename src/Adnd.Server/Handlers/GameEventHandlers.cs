using MediatR;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace Adnd.Server.Handlers;

/// <summary>
/// Handles game lifecycle events — activates/pauses the GameAgent.
/// </summary>
public class GameLifecycleHandler :
    INotificationHandler<GameCreated>,
    INotificationHandler<GameStarted>,
    INotificationHandler<GameArchived>,
    INotificationHandler<GamePaused>,
    INotificationHandler<GameResumed>,
    INotificationHandler<GameNarrationStarted>
{
    private readonly IGameAgentManager _gameAgentManager;
    private readonly IAgentBus _agentBus;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHubContext<Adnd.Server.Hubs.GameHub> _hubContext;
    private readonly ILogger<GameLifecycleHandler> _logger;

    public GameLifecycleHandler(IGameAgentManager gameAgentManager, IAgentBus agentBus, IServiceScopeFactory serviceScopeFactory, IHubContext<Adnd.Server.Hubs.GameHub> hubContext, ILogger<GameLifecycleHandler> logger)
    {
        _gameAgentManager = gameAgentManager;
        _agentBus = agentBus;
        _serviceScopeFactory = serviceScopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task Handle(GameCreated notification, CancellationToken ct)
    {
        _logger.LogInformation("[STATE] GameCreated | GameId={GameId} | CreatorId={CreatorId} | SystemId={SystemId} | LLMPresetId={LLMPresetId}",
            notification.GameId, notification.CreatorId, notification.SystemId, notification.LLMPresetId);
        return Task.CompletedTask;
    }

    public async Task Handle(GameStarted notification, CancellationToken ct)
    {
        _logger.LogInformation("[STATE] GameStarted | GameId={GameId} | CreatorId={CreatorId} | Transition: Draft→Starting", 
            notification.GameId, notification.CreatorId);

        var agent = _gameAgentManager.GetOrCreate(notification.GameId);
        await agent.StartAsync(notification.GameId, notification.CreatorId);
        _logger.LogInformation("[STATE] GameAgentStarted | GameId={GameId} | Status=Running | Loop=Started",
            notification.GameId);

        // Queue the initial GM narrative call so the processing loop has something to process
        try
        {
            var call = await _agentBus.ActivateGameAgentAsync(notification.GameId, notification.CreatorId);
            _logger.LogInformation("[AGENT_CALL] QueuedInitialNarrate | GameId={GameId} | CallId={CallId} | Action={Action} | Status={Status}",
                notification.GameId, call.Id, call.Action, call.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AGENT_CALL] FailedToQueueInitialNarrate | GameId={GameId} | Error={Error}",
                notification.GameId, ex.Message);
        }
    }

    public async Task Handle(GameArchived notification, CancellationToken ct)
    {
        _logger.LogInformation("[STATE] GameArchived | GameId={GameId} | Transition: Active→Archived | Agent=Removed",
            notification.GameId);
        _gameAgentManager.Remove(notification.GameId);
    }

    public async Task Handle(GamePaused notification, CancellationToken ct)
    {
        _logger.LogInformation("[STATE] GamePaused | GameId={GameId} | Transition: Running→Paused | Agent=Paused",
            notification.GameId);
        var agent = _gameAgentManager.GetOrCreate(notification.GameId);
        await agent.PauseAsync(notification.GameId);
    }

    public async Task Handle(GameNarrationStarted notification, CancellationToken ct)
    {
        _logger.LogInformation("[STATE] GameNarrationStarted | GameId={GameId} | MessageId={MessageId} | Transition: Starting→Active",
            notification.GameId, notification.MessageId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await context.Games.FindAsync(notification.GameId);
        if (game != null && game.Status == Models.GameStatus.Starting)
        {
            game.Status = Models.GameStatus.Active;
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("[STATE] GameActive | GameId={GameId} | Transition: Starting→Active", notification.GameId);

            // Broadcast status change to frontend
            await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GameStatusChanged", new
            {
                GameId = game.Id,
                Status = game.Status,
                ChangedAt = DateTime.UtcNow
            });
        }
    }

    public async Task Handle(GameResumed notification, CancellationToken ct)
    {
        _logger.LogInformation("[STATE] GameResumed | GameId={GameId} | Transition: Paused→Running", notification.GameId);
        var agent = _gameAgentManager.GetOrCreate(notification.GameId);

        var wasRestarted = agent.IsActive(notification.GameId);
        await agent.ResumeAsync(notification.GameId);

        var isNowActive = agent.IsActive(notification.GameId);
        _logger.LogInformation("[STATE] GameAgentResumed | GameId={GameId} | LoopRestarted={WasRestarted} | NowActive={IsActive}",
            notification.GameId, !wasRestarted && isNowActive, isNowActive);
    }
}

/// <summary>
/// Handles player lifecycle events.
/// Responsibility: Log player state changes for observability.
/// Note: PlotWeaverHandler reacts to PlayerJoined for plot thread generation.
///       PlayerDisconnectDetector was removed — GameHub handles disconnect detection.
/// </summary>
public class PlayerHandler :
    INotificationHandler<PlayerJoined>,
    INotificationHandler<PlayerLeft>,
    INotificationHandler<PlayerDisconnected>,
    INotificationHandler<PlayerReconnected>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlayerHandler> _logger;

    public PlayerHandler(IServiceScopeFactory scopeFactory, ILogger<PlayerHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task Handle(PlayerJoined notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLAYER] Joined | GameId={GameId} | PlayerId={PlayerId} | Character={Character} | Role={Role}",
            notification.GameId, notification.PlayerId, notification.CharacterName, notification.PlayerId);
        return Task.CompletedTask;
    }

    public Task Handle(PlayerLeft notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLAYER] Left | GameId={GameId} | PlayerId={PlayerId}",
            notification.GameId, notification.PlayerId);
        return Task.CompletedTask;
    }

    public async Task Handle(PlayerDisconnected notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLAYER] Disconnected | GameId={GameId} | PlayerId={PlayerId} | Character={Character} | UserId={UserId} | DisconnectedAt={DisconnectedAt}",
            notification.GameId, notification.PlayerId, notification.CharacterName, notification.UserId, notification.DisconnectedAt);

        // If during combat, log that the player's participant may be AFK
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var combat = await context.Combats
                .FirstOrDefaultAsync(c => c.GameId == notification.GameId && c.Status == Models.CombatStatus.Active, ct);

            if (combat != null)
            {
                var participant = await context.CombatParticipants
                    .FirstOrDefaultAsync(p => p.CombatId == combat.Id && p.PlayerId == notification.PlayerId, ct);

                if (participant != null)
                {
                    _logger.LogInformation("[PLAYER] CombatDisconnect | GameId={GameId} | PlayerId={PlayerId} | CombatId={CombatId} | Participant={ParticipantId} — may be AFK",
                        notification.GameId, notification.PlayerId, combat.Id, participant.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PLAYER] Failed to check combat state for disconnected player {PlayerId}", notification.PlayerId);
        }
    }

    public Task Handle(PlayerReconnected notification, CancellationToken ct)
    {
        _logger.LogInformation("[PLAYER] Reconnected | GameId={GameId} | PlayerId={PlayerId} | Character={Character} | UserId={UserId}",
            notification.GameId, notification.PlayerId, notification.CharacterName, notification.UserId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles game action events — queues GM narrative calls for events that need
/// flavor text or story response. Pure mechanics (dice rolls, initiative, grid)
/// are handled by the Hub directly and do not trigger the GM.
/// </summary>
public class GameActionHandler :
    INotificationHandler<SkillCheckRequested>,
    INotificationHandler<AttackRequested>,
    INotificationHandler<CombatStarted>,
    INotificationHandler<CombatEnded>,
    INotificationHandler<StorySwayed>,
    INotificationHandler<CombatAttackExecuted>,
    INotificationHandler<CombatSaveThrowExecuted>,
    INotificationHandler<CombatSpellCast>,
    INotificationHandler<CombatDamageDealt>,
    INotificationHandler<CombatHealed>,
    INotificationHandler<CombatXPGranted>,
    INotificationHandler<CombatLevelUp>,
    INotificationHandler<CombatRestStarted>,
    INotificationHandler<CombatRestEnded>
{
    private readonly IAgentBus _agentBus;
    private readonly AppDbContext _context;
    private readonly ILogger<GameActionHandler> _logger;

    public GameActionHandler(IAgentBus agentBus, AppDbContext context, ILogger<GameActionHandler> logger)
    {
        _agentBus = agentBus;
        _context = context;
        _logger = logger;
    }

    public async Task Handle(SkillCheckRequested n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, n.SessionId, $"Skill check: {n.Skill} (DC {n.DC}) by player {n.PlayerId}");
    }

    public async Task Handle(AttackRequested n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, n.SessionId, $"Attack: {n.Weapon} vs {n.Target} by player {n.PlayerId}");
    }

    public async Task Handle(CombatStarted n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, n.SessionId, $"Combat started: {n.Name ?? "Unnamed encounter"}");
    }

    public async Task Handle(CombatEnded n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Combat ended: {n.Result ?? "Unknown outcome"}");
    }

    public async Task Handle(StorySwayed n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Story sway from creator: {n.Direction}");
    }

    public async Task Handle(CombatAttackExecuted n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Combat attack: {n.Attacker} uses {n.Weapon} on {n.Target}");
    }

    public async Task Handle(CombatSaveThrowExecuted n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Save/throw: {n.Participant} rolls {n.SaveType} (DC {n.DC})");
    }

    public async Task Handle(CombatSpellCast n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Spell cast: {n.Caster} casts {n.SpellName} on {n.Target} (DC {n.SaveDC})");
    }

    public async Task Handle(CombatDamageDealt n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Damage: {n.ParticipantId} takes {n.Damage} damage from {n.Source}");
    }

    public async Task Handle(CombatHealed n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Healing: {n.ParticipantId} heals {n.Amount} HP from {n.Source}");
    }

    public async Task Handle(CombatXPGranted n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"XP granted: {n.ParticipantId} gains {n.XP} XP ({n.Reason})");
    }

    public async Task Handle(CombatLevelUp n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Level up: {n.ParticipantId} reaches level {n.NewLevel} ({n.SystemId})");
    }

    public async Task Handle(CombatRestStarted n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, $"Rest started: {n.RestType}");
    }

    public async Task Handle(CombatRestEnded n, CancellationToken ct)
    {
        await QueueGMMaybe(n.GameId, null, "Rest ended");
    }

    /// <summary>
    /// Queue a GM narrative call only if the game agent is active.
    /// Uses a short debounce to avoid spamming on rapid-fire actions.
    /// </summary>
    private async Task QueueGMMaybe(Guid gameId, Guid? sessionId, string context)
    {
        var gameStatus = await _agentBus.GetGMStatusAsync(gameId);
        if (gameStatus.Status != GMStatus.Running)
        {
            _logger.LogDebug("[AGENT_CALL] SkipNarrateQueue | GameId={GameId} | GMStatus={GMStatus} — no narrative needed",
                gameId, gameStatus.Status);
            return; // GM is paused or idle — no narrative needed
        }

        // Quick debounce: if there's already a pending GM call, don't add another.
        // This prevents spam during rapid combat actions.
        var pendingCount = await _agentBus.GetPendingCallsAsync(AgentType.GM, 5);
        var gmPending = pendingCount.Where(c => c.Action == AgentAction.Narrate).Count();
        if (gmPending >= 2)
        {
            _logger.LogDebug("[AGENT_CALL] DebounceSkip | GameId={GameId} | PendingNarrates={Count} | Context={Context}",
                gameId, gmPending, context);
            return;
        }

        // Load game entity to get the language setting
        var game = await _context.Games.FindAsync(gameId);
        var languageSuffix = string.IsNullOrEmpty(game?.Language) || game.Language == "English" ? "" :
            $"\n\n**Language**: All narrative output must be in **{game.Language}**. Write your response entirely in {game.Language}. Do NOT use English for any narrative content.";

        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = sessionId,
            FromAgent = AgentType.System,
            ToAgent = AgentType.GM,
            Action = AgentAction.Narrate,
            Input = JsonSerializer.Serialize(new GMDispatchOptions
            {
                SystemPrompt = $"You are the Game Master for a TTRPG session. " +
                    $"A game action just occurred. Provide vivid, immersive narrative flavor " +
                    $"for this event. Describe the sensory details, the atmosphere, and the " +
                    $"immediate reaction of the environment and NPCs. " +
                    $"Do NOT describe the mechanical result — the players already know the numbers. " +
                    $"Focus on the story moment. Keep it to 1-2 paragraphs. " +
                    $"Game system: {gameStatus.Status}. " +
                    $"Current game state: {gameStatus.LastAction ?? "N/A"}." + languageSuffix,
                UserPrompt = context
            }),
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var queuedCall = await _agentBus.SendCallAsync(call);
        _logger.LogInformation("[AGENT_CALL] QueuedNarrate | GameId={GameId} | CallId={CallId} | From={FromAgent} → To={ToAgent} [{Action}] | Context={Context} | PendingNarrates={PendingCount}",
            gameId, queuedCall.Id, queuedCall.FromAgent, queuedCall.ToAgent, queuedCall.Action, context, gmPending + 1);
    }
}

/// <summary>
/// Handles chat/whisper events.
/// Responsibility: Log message routing for observability.
/// Note: OOC bypass is enforced at the Hub level (separate events published for OOC vs in-game).
///       The PlotWeaverHandler reacts to MessageSent for periodic plot reviews.
/// </summary>
public class ChatHandler :
    INotificationHandler<MessageSent>,
    INotificationHandler<WhisperSent>,
    INotificationHandler<OOCMessageSent>,
    INotificationHandler<OOCWhisperSent>,
    INotificationHandler<OOCWhisperReceived>
{
    private readonly ILogger<ChatHandler> _logger;

    public ChatHandler(ILogger<ChatHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(MessageSent notification, CancellationToken ct)
    {
        _logger.LogInformation("[CHAT] MessageSent | GameId={GameId} | Type={Type} | OOC={IsOOC} | PlayerId={PlayerId}",
            notification.GameId, notification.Type, notification.IsOOC, notification.PlayerId);
        return Task.CompletedTask;
    }

    public Task Handle(WhisperSent notification, CancellationToken ct)
    {
        _logger.LogInformation("[CHAT] WhisperSent | GameId={GameId} | From={FromPlayerId} → Targets={Targets} | Type={Type}",
            notification.GameId, notification.FromPlayerId, notification.Targets, notification.Type);
        return Task.CompletedTask;
    }

    public Task Handle(OOCMessageSent notification, CancellationToken ct)
    {
        _logger.LogInformation("[CHAT] OOCMessageSent | GameId={GameId} | Channel={Channel} | PlayerId={PlayerId}",
            notification.GameId, notification.OOCChannel, notification.PlayerId);
        return Task.CompletedTask;
    }

    public Task Handle(OOCWhisperSent notification, CancellationToken ct)
    {
        _logger.LogInformation("[CHAT] OOCWhisperSent | GameId={GameId} | From={FromPlayerId} → Targets={Targets}",
            notification.GameId, notification.FromPlayerId, notification.Targets);
        return Task.CompletedTask;
    }

    public Task Handle(OOCWhisperReceived notification, CancellationToken ct)
    {
        _logger.LogInformation("[CHAT] OOCWhisperReceived | GameId={GameId} | From={FromPlayerId} → To={ToPlayerId}",
            notification.GameId, notification.FromPlayerId, notification.ToPlayerId);
        return Task.CompletedTask;
    }
}



/// <summary>
/// Handles session events.
/// </summary>
public class SessionHandler :
    INotificationHandler<SessionCreated>,
    INotificationHandler<SessionClosed>
{
    private readonly ILogger<SessionHandler> _logger;

    public SessionHandler(ILogger<SessionHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SessionCreated notification, CancellationToken ct)
    {
        _logger.LogInformation("Session created: {SessionId} in game {GameId}",
            notification.SessionId, notification.GameId);
        return Task.CompletedTask;
    }

    public Task Handle(SessionClosed notification, CancellationToken ct)
    {
        _logger.LogInformation("Session closed: {SessionId} in game {GameId}",
            notification.SessionId, notification.GameId);
        return Task.CompletedTask;
    }
}
