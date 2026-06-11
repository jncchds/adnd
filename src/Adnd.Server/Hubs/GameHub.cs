using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Adnd.Server.Hubs;

public partial class GameHub : Hub
{
    // In-memory player-to-connection mapping (use Redis/DistributedCache in production)
    private static readonly ConcurrentDictionary<string, string> _playerConnections = new();

    private readonly AppDbContext _context;
    private readonly IGameEngine _gameEngine;
    private readonly IAgentBus _agentBus;
    private readonly IWhisperService _whisperService;
    private readonly ICombatService _combatService;
    private readonly IGMToolRegistry _toolRegistry;
    private readonly IMediator _mediator;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(AppDbContext context, IGameEngine gameEngine, IAgentBus agentBus,
        IWhisperService whisperService, ICombatService combatService, IGMToolRegistry toolRegistry,
        IMediator mediator, IEmbeddingService embeddingService, ILogger<GameHub> logger)
    {
        _context = context;
        _gameEngine = gameEngine;
        _agentBus = agentBus;
        _whisperService = whisperService;
        _combatService = combatService;
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast GM status change to all players in a game.
    /// </summary>
    public async Task BroadcastGMStatusAsync(Guid gameId, Models.GMStatus status, string? lastAction)
    {
        await _mediator.Publish(new GMStatusChanged(gameId, status, lastAction));
        await Clients.Group(gameId.ToString()).SendAsync("GMStatusChanged", new
        {
            GameId = gameId,
            Status = status,
            LastAction = lastAction,
            ChangedAt = DateTime.UtcNow
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

        // Find all players connected via this connection and mark as disconnected
        var disconnectedPlayers = await _context.Players
            .Where(p => p.Status == PlayerStatus.Active)
            .ToListAsync();

        foreach (var player in disconnectedPlayers)
        {
            var connectionId = _playerConnections.GetValueOrDefault(player.Id.ToString());
            if (connectionId == Context.ConnectionId)
            {
                player.Status = PlayerStatus.Disconnected;
                player.LeftAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        // Broadcast disconnection events for each affected player
        foreach (var player in disconnectedPlayers.Where(p => p.Status == PlayerStatus.Disconnected && p.LeftAt.HasValue))
        {
            await Clients.Group(player.GameId.ToString()).SendAsync("PlayerDisconnected", new
            {
                PlayerId = player.Id,
                UserId = player.UserId,
                CharacterName = player.CharacterName,
                GameId = player.GameId,
                Message = $"{player.CharacterName} has been disconnected",
                DisconnectedAt = player.LeftAt
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ==================== Heartbeat / Disconnection Detection ====================

    /// <summary>
    /// Check for players who have been disconnected (no heartbeat within timeout period).
    /// Call this periodically from a background service or timer.
    /// </summary>
    public async Task CheckDisconnectedPlayersAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);

        var stalePlayers = await _context.Players
            .Where(p => p.Status == PlayerStatus.Active)
            .ToListAsync();

        foreach (var player in stalePlayers)
        {
            var connectionId = _playerConnections.GetValueOrDefault(player.Id.ToString());
            if (connectionId == null)
            {
                // Player has no active connection — mark as disconnected
                player.Status = PlayerStatus.Disconnected;
                player.LeftAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await Clients.Group(player.GameId.ToString()).SendAsync("PlayerDisconnected", new
                {
                    PlayerId = player.Id,
                    UserId = player.UserId,
                    CharacterName = player.CharacterName,
                    GameId = player.GameId,
                    Message = $"{player.CharacterName} has been disconnected",
                    DisconnectedAt = player.LeftAt
                });

                _logger.LogInformation("Player {CharacterName} ({UserId}) disconnected (no active connection) in game {GameId}",
                    player.CharacterName, player.UserId, player.GameId);
            }
        }
    }

    /// <summary>
    /// Player sends a heartbeat to indicate they are still connected.
    /// Resets their last-seen timestamp.
    /// </summary>
    public async Task SendHeartbeat(Guid gameId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            return;

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == uid && p.Status == PlayerStatus.Disconnected);

        if (player != null)
        {
            // Reconnect a previously disconnected player
            player.Status = PlayerStatus.Active;
            player.LeftAt = null;
            await _context.SaveChangesAsync();

            await Clients.Group(gameId.ToString()).SendAsync("PlayerReconnected", new
            {
                PlayerId = player.Id,
                UserId = player.UserId,
                CharacterName = player.CharacterName,
                Message = $"{player.CharacterName} has reconnected"
            });

            _logger.LogInformation("Player {CharacterName} ({UserId}) reconnected to game {GameId}",
                player.CharacterName, player.UserId, gameId);
        }
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
                message.Embedding = embedding;
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
            var message = new Adnd.Server.Models.Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId ?? Guid.Empty,
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
}
