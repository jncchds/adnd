using MediatR;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Data;
using Microsoft.Extensions.Logging;
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
            return; // GM is paused or idle — no narrative needed

        // Quick debounce: if there's already a pending GM call, don't add another.
        // This prevents spam during rapid combat actions.
        var pendingCount = await _agentBus.GetPendingCallsAsync(AgentType.GM, 5);
        var gmPending = pendingCount.Where(c => c.Action == AgentAction.Narrate).Count();
        if (gmPending >= 2)
        {
            _logger.LogDebug("Skipping GM narrative queue for game {GameId} — already {Count} pending",
                gameId, gmPending);
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

        await _agentBus.SendCallAsync(call);
        _logger.LogDebug("Queued GM narrative for game {GameId}: {Context}", gameId, context);
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
