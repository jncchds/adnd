using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using System.Text.Json;
using System.Collections.Concurrent;


namespace Adnd.Server.Hubs;

public partial class GameHub : Hub
{
    // In-memory player-to-connection mapping (use Redis/DistributedCache in production)
    // Bounded with periodic cleanup to prevent memory leak from stale entries
    private static readonly ConcurrentDictionary<string, string> _playerConnections = new();

    static GameHub()
    {
        _connectionCleanupTimer = new System.Timers.Timer(60000) { AutoReset = true };
        _connectionCleanupTimer.Elapsed += (_, __) => CleanupStaleConnections(null);
    }

    private static System.Timers.Timer _connectionCleanupTimer;

    private readonly AppDbContext _context;
    private readonly IGameEngine _gameEngine;
    private readonly IAgentBus _agentBus;
    private readonly IWhisperService _whisperService;
    private readonly ICombatService _combatService;
    private readonly IGMToolRegistry _toolRegistry;
    private readonly IEventBus _eventBus;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(AppDbContext context, IGameEngine gameEngine, IAgentBus agentBus,
        IWhisperService whisperService, ICombatService combatService, IGMToolRegistry toolRegistry,
        IEventBus mediator, IEmbeddingService embeddingService, ILogger<GameHub> logger)
    {
        _context = context;
        _gameEngine = gameEngine;
        _agentBus = agentBus;
        _whisperService = whisperService;
        _combatService = combatService;
        _toolRegistry = toolRegistry;
        _eventBus = mediator;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Publish a notification asynchronously to avoid blocking the SignalR Hub caller.
    /// Critical events (player join/leave) should still use await.
    /// Slow events (GM narrative queue) should use this.
    /// </summary>
    private void PublishAsync<T>(T notification, CancellationToken ct = default) where T : IGameEvent
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _eventBus.PublishAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing async event {EventType}", typeof(T).Name);
            }
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
        {
            // Find the player record for this user — no status filter needed
            // (connection status is tracked via SignalR groups, not DB)
            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.UserId == uid);

            if (player != null)
            {
                var wasAlreadyConnected = _playerConnections.ContainsKey(player.Id.ToString());
                _playerConnections.AddOrUpdate(player.Id.ToString(), Context.ConnectionId, (k, oldValue) => Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, player.GameId.ToString());

                if (wasAlreadyConnected)
                {
                    // Player reconnected after a disconnect
                    await _eventBus.PublishAsync(new PlayerReconnected(player.GameId, player.Id, player.UserId));
                }
                else
                {
                    // First connection
                    await _eventBus.PublishAsync(new PlayerJoined(player.GameId, player.Id, player.UserId, player.CharacterName ?? "Unknown"));
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

        // Reverse lookup: find the player ID from the in-memory map first,
        // then query the DB by ID — avoids EF Core translating the dictionary call.
        var playerEntry = _playerConnections.FirstOrDefault(kv => kv.Value == Context.ConnectionId);
        if (playerEntry.Key == null || !Guid.TryParse(playerEntry.Key, out var playerGuid))
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }
        var player = await _context.Players.FindAsync(playerGuid);

        if (player != null)
        {
            // Remove from connections
            _playerConnections.TryRemove(player.Id.ToString(), out _);

            // Leave game group — connection status is now tracked via SignalR groups, not DB
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, player.GameId.ToString());

            _logger.LogInformation("Player {CharacterName} ({UserId}) disconnected from game {GameId}",
                player.CharacterName, player.UserId, player.GameId);

            // Publish disconnect event for UI updates and observability
            await _eventBus.PublishAsync(new PlayerDisconnected(player.GameId, player.Id, player.UserId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ==================== Connection Tracking ====================

    /// <summary>
    /// Check if a player is currently connected to the game.
    /// Connection status is tracked via SignalR groups, not DB.
    /// </summary>
    public static bool IsPlayerConnected(Guid gameId, Guid playerId)
    {
        return _playerConnections.ContainsKey(playerId.ToString());
    }

    /// <summary>
    /// Get all connected player IDs for a game.
    /// Connection status is tracked via SignalR groups, not DB.
    /// </summary>
    public static List<Guid> IsConnectedPlayers(Guid gameId)
    {
        return _playerConnections
            .Where(kvp => kvp.Value != "stale")
            .Select(kvp => Guid.TryParse(kvp.Key, out var pid) ? pid : Guid.Empty)
            .Where(pid => pid != Guid.Empty)
            .ToList();
    }

    /// <summary>
    /// Generate an embedding for a message asynchronously.
    /// Called after messages are saved to the database.
    /// </summary>
    private async Task EmbedMessageAsync(Guid gameId, Guid messageId, string content)
    {
        try
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(gameId, content);
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null && message.Embedding == null)
            {
                message.Embedding = embedding != null ? new Vector(embedding) : null;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for message {MessageId} in game {GameId}", messageId, gameId);
        }
    }

    // ==================== Unified Message Persistence ====================

    /// <summary>
    /// Persist a game event as a Message record in the database for unified chat history.
    /// This enables all game events (combat, dice, agent calls, etc.) to appear in the chat.
    /// </summary>
    private async Task PersistGameEventAsync(
        Guid gameId,
        Guid? sessionId,
        Guid? playerId,
        string senderName,
        string content,
        Adnd.Server.Models.MessageType messageType,
        JsonElement? metadata = null,
        bool isOOC = false)
    {
        try
        {
            // Use game's current session when sessionId is not provided
            var resolvedSession = sessionId ?? await ResolveGameSessionAsync(gameId);

            var message = new Adnd.Server.Models.Message
            {
                Id = Guid.NewGuid(),
                SessionId = resolvedSession,
                PlayerId = playerId,
                Content = content,
                Type = messageType,
                IsOOC = isOOC,
                Metadata = metadata ?? JsonDocument.Parse("{}").RootElement,
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Generate embedding for narrative-influencing messages
            if (!isOOC && !string.IsNullOrEmpty(content))
            {
                await EmbedMessageAsync(gameId, message.Id, content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist game event of type {MessageType} in game {GameId}", messageType, gameId);
        }
    }

    /// <summary>
    /// Resolve the game's current session ID (auto-created, single per game).
    /// </summary>
    private async Task<Guid> ResolveGameSessionAsync(Guid gameId)
    {
        var game = await _context.Games
            .Where(g => g.Id == gameId)
            .Select(g => g.CurrentSessionId)
            .FirstOrDefaultAsync();
        return game ?? Guid.Empty;
    }

    // ==================== Connection Cleanup ====================

    private static readonly ILogger _cleanupLogger = Microsoft.Extensions.Logging.LoggerFactory
        .Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information))
        .CreateLogger<GameHub>();

    /// <summary>
    /// Periodically removes stale connections from _playerConnections.
    /// Runs every 60 seconds to prevent memory leak from clients that disconnected without proper cleanup.
    /// "Stale" entries are set when OnDisconnectedAsync fires unexpectedly.
    /// </summary>
    private static void CleanupStaleConnections(object? state)
    {
        var staleCount = 0;
        foreach (var kvp in _playerConnections)
        {
            // Only clean up entries explicitly marked as stale
            if (kvp.Value == "stale")
            {
                _playerConnections.TryRemove(kvp.Key, out _);
                staleCount++;
            }
        }

        if (staleCount > 0)
        {
            _cleanupLogger.LogInformation("Cleaned up {Count} stale SignalR connections", staleCount);
        }
    }
}
