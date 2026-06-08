using MediatR;
using Adnd.Server.Events;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

/// <summary>
/// Handles game lifecycle events — activates/pauses the GameAgent.
/// </summary>
public class GameLifecycleHandler :
    INotificationHandler<GameCreated>,
    INotificationHandler<GameStarted>,
    INotificationHandler<GameArchived>,
    INotificationHandler<GamePaused>,
    INotificationHandler<GameResumed>
{
    private readonly IGameAgentManager _gameAgentManager;
    private readonly IAgentBus _agentBus;
    private readonly ILogger<GameLifecycleHandler> _logger;

    public GameLifecycleHandler(IGameAgentManager gameAgentManager, IAgentBus agentBus, ILogger<GameLifecycleHandler> logger)
    {
        _gameAgentManager = gameAgentManager;
        _agentBus = agentBus;
        _logger = logger;
    }

    public Task Handle(GameCreated notification, CancellationToken ct)
    {
        _logger.LogInformation("Game created: {GameId} by {CreatorId}", notification.GameId, notification.CreatorId);
        return Task.CompletedTask;
    }

    public async Task Handle(GameStarted notification, CancellationToken ct)
    {
        _logger.LogInformation("Game started — activating GameAgent: {GameId}", notification.GameId);
        var agent = _gameAgentManager.GetOrCreate(notification.GameId);
        await agent.StartAsync(notification.GameId, notification.CreatorId);

        // Queue the initial GM narrative call so the processing loop has something to process
        try
        {
            await _agentBus.ActivateGameAgentAsync(notification.GameId, notification.CreatorId);
            _logger.LogInformation("Queued initial GM narrative for game {GameId}", notification.GameId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to queue initial GM narrative for game {GameId}", notification.GameId);
        }
    }

    public async Task Handle(GameArchived notification, CancellationToken ct)
    {
        _logger.LogInformation("Game archived — removing GameAgent: {GameId}", notification.GameId);
        _gameAgentManager.Remove(notification.GameId);
    }

    public async Task Handle(GamePaused notification, CancellationToken ct)
    {
        _logger.LogInformation("Game paused — pausing GameAgent: {GameId}", notification.GameId);
        var agent = _gameAgentManager.GetOrCreate(notification.GameId);
        await agent.PauseAsync(notification.GameId);
    }

    public async Task Handle(GameResumed notification, CancellationToken ct)
    {
        _logger.LogInformation("Game resumed — resuming GameAgent: {GameId}", notification.GameId);
        var agent = _gameAgentManager.GetOrCreate(notification.GameId);
        await agent.ResumeAsync(notification.GameId);
    }
}

/// <summary>
/// Handles player lifecycle events.
/// </summary>
public class PlayerHandler :
    INotificationHandler<PlayerJoined>,
    INotificationHandler<PlayerLeft>
{
    private readonly ILogger<PlayerHandler> _logger;

    public PlayerHandler(ILogger<PlayerHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PlayerJoined notification, CancellationToken ct)
    {
        _logger.LogInformation("Player joined game {GameId}: {PlayerId}", notification.GameId, notification.PlayerId);
        return Task.CompletedTask;
    }

    public Task Handle(PlayerLeft notification, CancellationToken ct)
    {
        _logger.LogInformation("Player left game {GameId}: {PlayerId}", notification.GameId, notification.PlayerId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles game action events — each handler logs the event.
/// Game logic is handled directly by the hub calling services.
/// Narrative/LLM processing is handled by the GameAgent.
/// </summary>
public class GameActionHandler :
    INotificationHandler<DiceRolled>,
    INotificationHandler<SkillCheckRequested>,
    INotificationHandler<AttackRequested>,
    INotificationHandler<CombatStarted>,
    INotificationHandler<CombatEnded>,
    INotificationHandler<StorySwayed>,
    INotificationHandler<ParticipantAdded>,
    INotificationHandler<ParticipantRemoved>,
    INotificationHandler<InitiativeRolled>,
    INotificationHandler<InitiativeRolledForAll>,
    INotificationHandler<TurnAdvanced>,
    INotificationHandler<TurnRetreated>,
    INotificationHandler<CombatAttackExecuted>,
    INotificationHandler<CombatSaveThrowExecuted>,
    INotificationHandler<CombatSpellCast>,
    INotificationHandler<CombatDamageDealt>,
    INotificationHandler<CombatHealed>,
    INotificationHandler<CombatXPGranted>,
    INotificationHandler<CombatLevelUp>,
    INotificationHandler<CombatRestStarted>,
    INotificationHandler<CombatRestEnded>,
    INotificationHandler<CombatGridSet>,
    INotificationHandler<CombatPositionSet>,
    INotificationHandler<CombatMove>,
    INotificationHandler<CombatConditionApplied>,
    INotificationHandler<CombatConditionRemoved>
{
    private readonly ILogger<GameActionHandler> _logger;

    public GameActionHandler(ILogger<GameActionHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(DiceRolled n, CancellationToken ct) => Log(n, nameof(DiceRolled));
    public Task Handle(SkillCheckRequested n, CancellationToken ct) => Log(n, nameof(SkillCheckRequested));
    public Task Handle(AttackRequested n, CancellationToken ct) => Log(n, nameof(AttackRequested));
    public Task Handle(CombatStarted n, CancellationToken ct) => Log(n, nameof(CombatStarted));
    public Task Handle(CombatEnded n, CancellationToken ct) => Log(n, nameof(CombatEnded));
    public Task Handle(StorySwayed n, CancellationToken ct) => Log(n, nameof(StorySwayed));
    public Task Handle(ParticipantAdded n, CancellationToken ct) => Log(n, nameof(ParticipantAdded));
    public Task Handle(ParticipantRemoved n, CancellationToken ct) => Log(n, nameof(ParticipantRemoved));
    public Task Handle(InitiativeRolled n, CancellationToken ct) => Log(n, nameof(InitiativeRolled));
    public Task Handle(InitiativeRolledForAll n, CancellationToken ct) => Log(n, nameof(InitiativeRolledForAll));
    public Task Handle(TurnAdvanced n, CancellationToken ct) => Log(n, nameof(TurnAdvanced));
    public Task Handle(TurnRetreated n, CancellationToken ct) => Log(n, nameof(TurnRetreated));
    public Task Handle(CombatAttackExecuted n, CancellationToken ct) => Log(n, nameof(CombatAttackExecuted));
    public Task Handle(CombatSaveThrowExecuted n, CancellationToken ct) => Log(n, nameof(CombatSaveThrowExecuted));
    public Task Handle(CombatSpellCast n, CancellationToken ct) => Log(n, nameof(CombatSpellCast));
    public Task Handle(CombatDamageDealt n, CancellationToken ct) => Log(n, nameof(CombatDamageDealt));
    public Task Handle(CombatHealed n, CancellationToken ct) => Log(n, nameof(CombatHealed));
    public Task Handle(CombatXPGranted n, CancellationToken ct) => Log(n, nameof(CombatXPGranted));
    public Task Handle(CombatLevelUp n, CancellationToken ct) => Log(n, nameof(CombatLevelUp));
    public Task Handle(CombatRestStarted n, CancellationToken ct) => Log(n, nameof(CombatRestStarted));
    public Task Handle(CombatRestEnded n, CancellationToken ct) => Log(n, nameof(CombatRestEnded));
    public Task Handle(CombatGridSet n, CancellationToken ct) => Log(n, nameof(CombatGridSet));
    public Task Handle(CombatPositionSet n, CancellationToken ct) => Log(n, nameof(CombatPositionSet));
    public Task Handle(CombatMove n, CancellationToken ct) => Log(n, nameof(CombatMove));
    public Task Handle(CombatConditionApplied n, CancellationToken ct) => Log(n, nameof(CombatConditionApplied));
    public Task Handle(CombatConditionRemoved n, CancellationToken ct) => Log(n, nameof(CombatConditionRemoved));

    private Task Log<T>(T notification, string name) where T : INotification
    {
        _logger.LogDebug("Game action event: {EventName} for game {GameId}", name, notification.GetType().GetProperty("GameId")?.GetValue(notification));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles chat/whisper events.
/// In-game messages are processed by the GameAgent for narrative.
/// OOC messages bypass the GameAgent entirely.
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
        _logger.LogDebug("Message sent in game {GameId}: type={Type} ooc={IsOOC}",
            notification.GameId, notification.Type, notification.IsOOC);
        // OOC messages are NOT processed by the GameAgent — they bypass narrative entirely
        return Task.CompletedTask;
    }

    public Task Handle(WhisperSent notification, CancellationToken ct)
    {
        _logger.LogDebug("Whisper in game {GameId}: {FromPlayerId} → {Targets} (type: {Type})",
            notification.GameId, notification.FromPlayerId, notification.Targets, notification.Type);
        return Task.CompletedTask;
    }

    public Task Handle(OOCMessageSent notification, CancellationToken ct)
    {
        _logger.LogDebug("OOC message in game {GameId}: channel={Channel}",
            notification.GameId, notification.OOCChannel);
        // OOC messages never reach the GameAgent
        return Task.CompletedTask;
    }

    public Task Handle(OOCWhisperSent notification, CancellationToken ct)
    {
        _logger.LogDebug("OOC whisper in game {GameId}: {FromPlayerId} → {Targets}",
            notification.GameId, notification.FromPlayerId, notification.Targets);
        return Task.CompletedTask;
    }

    public Task Handle(OOCWhisperReceived notification, CancellationToken ct)
    {
        _logger.LogDebug("OOC whisper received in game {GameId}: {FromPlayerId} → {ToPlayerId}",
            notification.GameId, notification.FromPlayerId, notification.ToPlayerId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles plot/NPC events.
/// </summary>
public class PlotHandler :
    INotificationHandler<NPCCreated>,
    INotificationHandler<NPCUpdated>,
    INotificationHandler<NPCDeleted>,
    INotificationHandler<PlotThreadCreated>,
    INotificationHandler<PlotThreadUpdated>
{
    private readonly ILogger<PlotHandler> _logger;

    public PlotHandler(ILogger<PlotHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(NPCCreated n, CancellationToken ct) => Log(n, nameof(NPCCreated));
    public Task Handle(NPCUpdated n, CancellationToken ct) => Log(n, nameof(NPCUpdated));
    public Task Handle(NPCDeleted n, CancellationToken ct) => Log(n, nameof(NPCDeleted));
    public Task Handle(PlotThreadCreated n, CancellationToken ct) => Log(n, nameof(PlotThreadCreated));
    public Task Handle(PlotThreadUpdated n, CancellationToken ct) => Log(n, nameof(PlotThreadUpdated));

    private Task Log<T>(T notification, string name) where T : INotification
    {
        _logger.LogDebug("Plot event: {EventName} for game {GameId}", name, notification.GetType().GetProperty("GameId")?.GetValue(notification));
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
