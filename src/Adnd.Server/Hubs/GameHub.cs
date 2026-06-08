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

public class GameHub : Hub
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
    private readonly ILogger<GameHub> _logger;

    public GameHub(AppDbContext context, IGameEngine gameEngine, IAgentBus agentBus,
        IWhisperService whisperService, ICombatService combatService, IGMToolRegistry toolRegistry,
        IMediator mediator, ILogger<GameHub> logger)
    {
        _context = context;
        _gameEngine = gameEngine;
        _agentBus = agentBus;
        _whisperService = whisperService;
        _combatService = combatService;
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ==================== Game Join/Leave ====================

    public async Task JoinGame(Guid gameId)
    {
        // Verify user is a player in this game
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == uid);

        if (player == null || player.Game == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not a player in this game." });
            return;
        }

        if (player.Status != PlayerStatus.Active)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Player is not active in this game." });
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());
        await Groups.AddToGroupAsync(Context.ConnectionId, $"player:{uid}");

        // Track player-to-connection mapping
        _playerConnections[uid.ToString()] = Context.ConnectionId;

        await Clients.Group(gameId.ToString()).SendAsync("PlayerJoined", new
        {
            ConnectionId = Context.ConnectionId,
            UserId = uid,
            PlayerId = player.Id,
            CharacterName = player.CharacterName,
            Role = player.Role,
            Message = $"{player.CharacterName} joined the game"
        });

        _logger.LogInformation("Player {CharacterName} ({UserId}) joined game {GameId}",
            player.CharacterName, uid, gameId);
    }

    public async Task LeaveGame(Guid gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId.ToString());

        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
        {
            // Remove from player connection tracking
            _playerConnections.TryRemove(uid.ToString(), out _);

            await Clients.Group(gameId.ToString()).SendAsync("PlayerLeft", new
            {
                ConnectionId = Context.ConnectionId,
                UserId = uid,
                Message = $"Player {uid} left the game"
            });
        }
    }

    // ==================== Chat Messages ====================

    /// <summary>
    /// Send an in-game public message (part of game narrative).
    /// </summary>
    public async Task SendMessage(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.InGamePublic,
            IsOOC = false,
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Publish event for game agent processing (in-game only)
        await _mediator.Publish(new MessageSent(
            session.GameId, sessionId, player.Id, content, Adnd.Server.Events.MessageType.InGamePublic, null, false));

        await Clients.Group(session.GameId.ToString()).SendAsync("NewMessage", new
        {
            message.Id,
            message.SessionId,
            message.PlayerId,
            message.Content,
            message.Type,
            message.Metadata,
            message.IsOOC,
            WhisperFromId = (Guid?)null,
            WhisperToId = (Guid?)null,
            WhisperTarget = (string?)null,
            message.CreatedAt
        });
    }

    /// <summary>
    /// Send an in-game whisper (to GM — adds to GM knowledge).
    /// </summary>
    public async Task SendInGameWhisper(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.InGameWhisper,
            IsOOC = false,
            WhisperFromId = player.Id,
            WhisperTarget = "gm",
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Create whisper record for GM targeting
        var whisper = await _whisperService.SendWhisperAsync(
            session.GameId, sessionId, player.Id, "gm",
            Adnd.Server.Models.WhisperType.InGamePlayerToGM, content);

        // Publish whisper event (not message event — this is not public narrative)
        await _mediator.Publish(new WhisperSent(
            session.GameId, player.Id, "gm", content, Adnd.Server.Events.WhisperType.InGamePlayerToGM));

        // Send to sender (confirmation)
        await Clients.Caller.SendAsync("NewWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = player.CharacterName,
            FromRole = player.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        // Send to GM (Creator)
        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.Role == PlayerRole.Creator && p.Status == PlayerStatus.Active);

        if (gmPlayer != null)
        {
            var gmConnectionId = GetConnectionIdForPlayer(gmPlayer.Id);
            if (gmConnectionId != null)
            {
                await Clients.Client(gmConnectionId).SendAsync("NewWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = player.CharacterName,
                    FromRole = player.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
            }
        }

        _logger.LogInformation("In-game whisper from {CharacterName} to GM in game {GameId}",
            player.CharacterName, session.GameId);
    }

    /// <summary>
    /// Send an OOC public message (never influences narrative).
    /// </summary>
    public async Task SendOOCMessage(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.OOCPublic,
            IsOOC = true,
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Publish OOC event — NOT processed by GameAgent
        await _mediator.Publish(new OOCMessageSent(
            session.GameId, sessionId, player.Id, content, "public"));

        await Clients.Group(session.GameId.ToString()).SendAsync("NewOOCMessage", new
        {
            message.Id,
            message.SessionId,
            message.PlayerId,
            message.Content,
            message.Type,
            message.IsOOC,
            message.CreatedAt
        });
    }

    /// <summary>
    /// Send an OOC whisper from a player to the GM (for clarification).
    /// GM responds with OOCWhisper.
    /// </summary>
    public async Task SendOOCWhisper(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.OOCWhisper,
            IsOOC = true,
            WhisperFromId = player.Id,
            WhisperTarget = "gm",
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Create whisper record
        var whisper = await _whisperService.SendWhisperAsync(
            session.GameId, sessionId, player.Id, "gm",
            Adnd.Server.Models.WhisperType.OOCPlayerToGM, content);

        // Publish OOC whisper event — NOT processed by GameAgent
        await _mediator.Publish(new OOCWhisperSent(
            session.GameId, player.Id, "gm", content));

        // Send to sender (confirmation)
        await Clients.Caller.SendAsync("NewOOCWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = player.CharacterName,
            FromRole = player.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        // Send to GM (Creator)
        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.Role == PlayerRole.Creator && p.Status == PlayerStatus.Active);

        if (gmPlayer != null)
        {
            var gmConnectionId = GetConnectionIdForPlayer(gmPlayer.Id);
            if (gmConnectionId != null)
            {
                await Clients.Client(gmConnectionId).SendAsync("NewOOCWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = player.CharacterName,
                    FromRole = player.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
            }
        }

        _logger.LogInformation("OOC whisper from {CharacterName} to GM in game {GameId}",
            player.CharacterName, session.GameId);
    }

    /// <summary>
    /// GM sends an OOC whisper to a player (e.g., clarifying rules).
    /// </summary>
    public async Task SendOOCWhisperToPlayer(Guid targetPlayerId, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the GM can send OOC whispers." });
            return;
        }

        var targetPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gmPlayer.GameId && p.Status == PlayerStatus.Active);

        if (targetPlayer == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Target player not found or inactive." });
            return;
        }

        var whisper = await _whisperService.SendGMWhisperAsync(
            gmPlayer.GameId, Guid.Empty, gmPlayer.Id,
            new List<Guid> { targetPlayerId }, Adnd.Server.Models.WhisperType.OOCGMToPlayer, content);

        // Send to the target player
        var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("NewOOCWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = gmPlayer.CharacterName,
                    FromRole = gmPlayer.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
        }

        // Confirmation to GM
        await Clients.Caller.SendAsync("NewOOCWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = gmPlayer.CharacterName,
            FromRole = gmPlayer.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        _logger.LogInformation("OOC whisper from GM to {Target} in game {GameId}",
            targetPlayer.CharacterName, gmPlayer.GameId);
    }

    /// <summary>
    /// GM sends an in-game whisper to a player (e.g., divination result).
    /// </summary>
    public async Task SendInGameWhisperToPlayer(Guid targetPlayerId, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the GM can send in-game whispers." });
            return;
        }

        var targetPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gmPlayer.GameId && p.Status == PlayerStatus.Active);

        if (targetPlayer == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Target player not found or inactive." });
            return;
        }

        var message = new Message
        {
            SessionId = Guid.Empty,
            PlayerId = gmPlayer.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.InGameWhisper,
            IsOOC = false,
            WhisperFromId = gmPlayer.Id,
            WhisperToId = targetPlayerId,
            WhisperTarget = $"player:{targetPlayerId}",
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Create whisper record
        var whisper = await _whisperService.SendGMWhisperAsync(
            gmPlayer.GameId, Guid.Empty, gmPlayer.Id,
            new List<Guid> { targetPlayerId }, Adnd.Server.Models.WhisperType.InGameGMToPlayer, content);

        // Send to the target player
        var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("NewWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = gmPlayer.CharacterName,
                    FromRole = gmPlayer.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
        }

        // Confirmation to GM
        await Clients.Caller.SendAsync("NewWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = gmPlayer.CharacterName,
            FromRole = gmPlayer.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        _logger.LogInformation("In-game whisper from GM to {Target} in game {GameId}",
            targetPlayer.CharacterName, gmPlayer.GameId);
    }

    // ==================== Whispers ====================

    /// <summary>
    /// Send a whisper from a player to specific target(s).
    /// Targets format: "player:{userId}" or "all" or "group:{groupName}"
    /// </summary>
    public async Task SendWhisper(string targets, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null || player.Game == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player." });
            return;
        }

        // Check whisper permission
        if (!_whisperService.CanWhisper(player))
        {
            await Clients.Caller.SendAsync("Error", new { message = "You do not have whisper permission." });
            return;
        }

        Adnd.Server.Models.WhisperType whisperType;
        if (targets == "all")
        {
            whisperType = player.Role == PlayerRole.Creator ? Adnd.Server.Models.WhisperType.GMToAll : Adnd.Server.Models.WhisperType.PlayerToPlayer;
        }
        else if (targets.Contains("player:", StringComparison.OrdinalIgnoreCase))
        {
            var isCreatorToPlayer = player.Role == PlayerRole.Creator;
            whisperType = isCreatorToPlayer ? Adnd.Server.Models.WhisperType.InGameGMToPlayer : Adnd.Server.Models.WhisperType.PlayerToPlayer;
        }
        else
        {
            whisperType = player.Role == PlayerRole.Creator ? Adnd.Server.Models.WhisperType.GMToGroup : Adnd.Server.Models.WhisperType.PlayerToPlayer;
        }

        var whisper = await _whisperService.SendWhisperAsync(
            player.GameId, Guid.Empty, player.Id, targets, whisperType, content);

        // Parse targets to determine who receives the whisper
        var targetIds = _whisperService.ParseTargets(targets);

        if (targets == "all" || string.IsNullOrEmpty(targets))
        {
            await Clients.Group(player.GameId.ToString()).SendAsync("NewWhisper", BuildWhisperResponse(whisper, player.CharacterName, player.Role));
        }
        else
        {
            foreach (var targetId in targetIds)
            {
                var targetPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == targetId && p.GameId == player.GameId);

                if (targetPlayer == null) continue;
                var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
                if (targetConnectionId != null)
                {
                    await Clients.Client(targetConnectionId)
                        .SendAsync("NewWhisper", BuildWhisperResponse(whisper, player.CharacterName, player.Role));
                }
            }

            await Clients.Caller.SendAsync("NewWhisper", new
            {
                whisper.Id,
                FromPlayerId = whisper.FromPlayerId,
                FromCharacter = player.CharacterName,
                FromRole = player.Role,
                Content = whisper.Content,
                Type = whisper.Type,
                Targets = whisper.Targets,
                CreatedAt = whisper.CreatedAt,
                IsSent = true
            });
        }

        _logger.LogInformation("Whisper from {CharacterName} to [{Targets}]: {Content}",
            player.CharacterName, targets, content);
    }

    /// <summary>
    /// GM sends a whisper to a specific player.
    /// </summary>
    public async Task SendGMWhisper(Guid targetPlayerId, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the game creator can send creator whispers." });
            return;
        }

        var targetPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gmPlayer.GameId && p.Status == PlayerStatus.Active);

        if (targetPlayer == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Target player not found or inactive." });
            return;
        }

        var whisper = await _whisperService.SendGMWhisperAsync(
            gmPlayer.GameId, Guid.Empty, gmPlayer.Id,
            new List<Guid> { targetPlayerId }, Adnd.Server.Models.WhisperType.InGameGMToPlayer, content);

        // Send to the target player
        var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("NewWhisper", BuildWhisperResponse(whisper, gmPlayer.CharacterName, gmPlayer.Role));
        }

        // Confirmation to GM
        await Clients.Caller.SendAsync("NewWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = gmPlayer.CharacterName,
            FromRole = gmPlayer.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        _logger.LogInformation("GM whisper from {GM} to {Target}: {Content}",
            gmPlayer.CharacterName, targetPlayer.CharacterName, content);
    }

    /// <summary>
    /// Get whisper history for the current player.
    /// </summary>
    public async Task<List<WhisperResponse>> GetWhisperHistory(Guid gameId, int limit = 50)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.GameId == gameId);

        if (player == null)
            throw new InvalidOperationException("Player not found in game.");

        var isCreator = player.Role == PlayerRole.Creator;
        var whispers = await _whisperService.GetWhispersForPlayerAsync(gameId, player.Id, isCreator, limit);

        return whispers.Select(w => new WhisperResponse
        {
            Id = w.Id,
            FromPlayerId = w.FromPlayerId,
            FromCharacter = w.FromPlayer?.CharacterName ?? "Unknown",
            FromRole = w.FromPlayer?.Role ?? PlayerRole.Player,
            Content = w.Content,
            Type = w.Type,
            Targets = w.Targets,
            CreatedAt = w.CreatedAt
        }).ToList();
    }

    // ==================== Agent Framework ====================

    /// <summary>
    /// Send an agent call from one agent to another.
    /// </summary>
    public async Task<AgentCallResponse> CallAgent(AgentType fromAgent, AgentType toAgent,
        AgentAction action, string input, Guid? sessionId = null)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        // Verify player is in the game
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
            throw new InvalidOperationException("Player not active in any game.");

        var call = new AgentCall
        {
            GameId = player.GameId,
            SessionId = sessionId,
            FromAgent = fromAgent,
            ToAgent = toAgent,
            Action = action,
            Input = input,
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Queue the call
        var queuedCall = await _agentBus.SendCallAsync(call);

        // Broadcast call started
        await Clients.Group(player.GameId.ToString()).SendAsync("AgentCallStarted", new
        {
            call.Id,
            call.FromAgent,
            call.ToAgent,
            call.Action,
            call.CreatedAt
        });

        // Execute the call
        var result = await _agentBus.ExecuteCallAsync(call);

        // Broadcast completion
        await Clients.Group(player.GameId.ToString()).SendAsync("AgentCallCompleted", new
        {
            call.Id,
            call.FromAgent,
            call.ToAgent,
            call.Action,
            call.Status,
            call.Output,
            call.OutputMessage,
            call.DurationMs,
            call.CompletedAt,
            call.Error
        });

        return new AgentCallResponse
        {
            Id = call.Id,
            FromAgent = call.FromAgent,
            ToAgent = call.ToAgent,
            Action = call.Action,
            Status = call.Status,
            Output = call.Output,
            OutputMessage = call.OutputMessage,
            DurationMs = call.DurationMs,
            Error = call.Error
        };
    }

    /// <summary>
    /// Get agent call history for a game.
    /// </summary>
    public async Task<List<AgentCallResponse>> GetAgentCallHistory(Guid gameId,
        AgentType? fromAgent = null, AgentAction? action = null, int limit = 50)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException("Game not found.");

        var hasAccess = game.CreatorId == uid || game.Players.Any(p => p.UserId == uid);
        if (!hasAccess)
            throw new ForbiddenException("You don't have access to this game.");

        var calls = await _agentBus.GetCallHistoryAsync(gameId, fromAgent, action, limit);

        return calls.Select(c => new AgentCallResponse
        {
            Id = c.Id,
            FromAgent = c.FromAgent,
            ToAgent = c.ToAgent,
            Action = c.Action,
            Status = c.Status,
            Output = c.Output,
            OutputMessage = c.OutputMessage,
            DurationMs = c.DurationMs,
            Error = c.Error,
            CreatedAt = c.CreatedAt,
            CompletedAt = c.CompletedAt
        }).ToList();
    }

    /// <summary>
    /// Get a specific agent call by ID.
    /// </summary>
    public async Task<AgentCallResponse> GetAgentCall(Guid callId)
    {
        var call = await _agentBus.GetCallAsync(callId);
        if (call == null)
            throw new KeyNotFoundException($"Agent call {callId} not found.");

        return new AgentCallResponse
        {
            Id = call.Id,
            FromAgent = call.FromAgent,
            ToAgent = call.ToAgent,
            Action = call.Action,
            Status = call.Status,
            Output = call.Output,
            OutputMessage = call.OutputMessage,
            DurationMs = call.DurationMs,
            Error = call.Error,
            CreatedAt = call.CreatedAt,
            CompletedAt = call.CompletedAt
        };
    }

    // ==================== Dice ====================

    public async Task RollDice(Guid sessionId, string formula, Guid? playerId = null)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var result = await _gameEngine.RollDiceAsync(sessionId, formula, playerId);

        // Publish event for game agent processing
        await _mediator.Publish(new DiceRolled(session.GameId, sessionId, formula, playerId));

        await Clients.Group(session.GameId.ToString()).SendAsync("DiceRollResult", new
        {
            Formula = result.Formula,
            DiceCount = result.DiceCount,
            DiceType = result.DiceType,
            Modifier = result.Modifier,
            Rolls = result.Rolls,
            FinalRolls = result.FinalRolls,
            Subtotal = result.Subtotal,
            Total = result.Total,
            PlayerId = playerId,
            Timestamp = result.RolledAt,
            FlavorText = GenerateFlavorText("dice", result.Formula, result.Total)
        });
    }

    // ==================== Skill Checks ====================

    public async Task SkillCheck(Guid sessionId, string skill, Guid? playerId = null, int? dc = null)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var result = await _gameEngine.SkillCheckAsync(sessionId, skill, playerId, dc);

        // Publish event for game agent processing
        await _mediator.Publish(new SkillCheckRequested(session.GameId, sessionId, skill, playerId, dc));

        await Clients.Group(session.GameId.ToString()).SendAsync("SkillCheckResult", new
        {
            Skill = result.Skill,
            DiceRoll = result.DiceRoll,
            Modifier = result.Modifier,
            Total = result.Total,
            DC = result.DC,
            Success = result.Success,
            RolledAt = result.RolledAt,
            FlavorText = GenerateFlavorText("skillcheck", result.Skill, result.Total, result.DC, result.Success)
        });
    }

    // ==================== Attacks ====================

    public async Task Attack(Guid sessionId, string weapon, string targetName, Guid? playerId = null)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var result = await _gameEngine.AttackAsync(sessionId, weapon, targetName, playerId);

        // Publish event for game agent processing
        await _mediator.Publish(new AttackRequested(session.GameId, sessionId, weapon, targetName, playerId));

        await Clients.Group(session.GameId.ToString()).SendAsync("AttackResult", new
        {
            Weapon = result.Weapon,
            Target = result.Target,
            Hit = result.Hit,
            AttackRoll = result.AttackRoll,
            AC = result.AC,
            DamageDice = result.DamageDice,
            DamageTotal = result.DamageTotal,
            RolledAt = result.RolledAt,
            FlavorText = GenerateFlavorText("attack", result.Weapon, result.Target, result.Hit, result.DamageTotal)
        });
    }

    // ==================== Combat ====================

    public async Task<CombatLogResponse> StartCombat(Guid gameId, Guid? sessionId, string? name = null)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
        {
            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.UserId == uid && p.GameId == gameId);
            if (player == null || player.Role != PlayerRole.Creator)
                throw new ForbiddenException("Only the GM can start combat.");
        }

        var combat = await _combatService.StartCombatAsync(gameId, sessionId, name);

        // Publish event for game agent processing
        await _mediator.Publish(new CombatStarted(gameId, sessionId, name));

        await Clients.Group(gameId.ToString()).SendAsync("CombatStarted", new
        {
            combat.Id,
            combat.Name,
            combat.CurrentRound,
            Participants = combat.Participants.Select(p => new { p.Id, p.DisplayName, p.ParticipantType, p.CurrentHP, p.MaxHP, p.AC, p.Initiative }).ToList(),
            combat.StartedAt,
            FlavorText = $"Combat begins: {combat.Name ?? "An unexpected encounter!"}"
        });

        return BuildCombatLog(combat);
    }

    public async Task<CombatLogResponse> EndCombat(Guid combatId, string? result = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var endedCombat = await _combatService.EndCombatAsync(combatId, result);

        // Publish event for game agent processing
        await _mediator.Publish(new CombatEnded(combat.GameId, combatId, result));

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatEnded", new
        {
            combatId,
            result,
            EndedAt = DateTime.UtcNow,
            FlavorText = result ?? "The combat ends."
        });

        return BuildCombatLog(endedCombat);
    }

    public async Task<CombatLogResponse> PauseCombat(Guid combatId)
    {
        var combat = await _combatService.PauseCombatAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatPaused", new { combatId });
        return BuildCombatLog(combat);
    }

    public async Task<CombatLogResponse> ResumeCombat(Guid combatId)
    {
        var combat = await _combatService.ResumeCombatAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatResumed", new { combatId });
        return BuildCombatLog(combat);
    }

    public async Task<CombatParticipantResponse> AddParticipant(Guid combatId, string participantType,
        string displayName, int ac, int currentHP, int maxHP, Guid? playerId = null, Guid? npcId = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = await _combatService.AddParticipantAsync(
            combatId, participantType, playerId, npcId, displayName, ac, currentHP, maxHP);

        // Publish event for game agent processing
        await _mediator.Publish(new ParticipantAdded(
            combat.GameId, combatId, participantType, displayName, ac, currentHP, maxHP, playerId, npcId));

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatParticipantAdded", new
        {
            participant.Id,
            participant.DisplayName,
            participant.ParticipantType,
            participant.CurrentHP,
            participant.MaxHP,
            participant.AC,
            participant.Initiative
        });

        return BuildParticipantResponse(participant);
    }

    public async Task<CombatLogResponse> RemoveParticipant(Guid combatId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RemoveParticipantAsync(combatId, participantId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatParticipantRemoved", new { participantId });
        return BuildCombatLog(result);
    }

    public async Task<(CombatParticipantResponse participant, int[] rolls)> RollInitiative(Guid combatId, Guid participantId, string formula = "1d20")
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var (participant, rolls) = await _combatService.RollInitiativeAsync(combatId, participantId, formula);

        await Clients.Group(combat.GameId.ToString()).SendAsync("InitiativeRolled", new
        {
            participant.Id,
            participant.DisplayName,
            participant.Initiative,
            rolls,
            FlavorText = $"{participant.DisplayName} rolls initiative: {participant.Initiative}"
        });

        return (BuildParticipantResponse(participant), rolls);
    }

    public async Task<CombatLogResponse> RollInitiativeForAll(Guid combatId, string formula = "1d20")
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RollInitiativeForAllAsync(combatId, formula);

        var turnOrder = string.Join(", ", result.Participants.Select(p => $"{p.DisplayName} ({p.Initiative})"));
        await Clients.Group(combat.GameId.ToString()).SendAsync("InitiativeComplete", new
        {
            turnOrder,
            Participants = result.Participants.Select(p => new
            {
                p.Id,
                p.DisplayName,
                p.Initiative,
                p.CurrentHP,
                p.MaxHP,
                p.AC
            }),
            FlavorText = $"Initiative order: {turnOrder}"
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> AdvanceTurn(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AdvanceTurnAsync(combatId);
        var currentTurn = await _combatService.GetCurrentTurnParticipantAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("TurnAdvanced", new
        {
            result.CurrentRound,
            currentTurn.Id,
            currentTurn.DisplayName,
            currentTurn.CurrentHP,
            currentTurn.MaxHP,
            currentTurn.AC,
            currentTurn.Initiative
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> RetreatTurn(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RetreatTurnAsync(combatId);
        var currentTurn = await _combatService.GetCurrentTurnParticipantAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("TurnRetreated", new
        {
            result.CurrentRound,
            currentTurn.Id,
            currentTurn.DisplayName
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatParticipantResponse> GetCurrentTurn(Guid combatId)
    {
        var participant = await _combatService.GetCurrentTurnParticipantAsync(combatId);
        return BuildParticipantResponse(participant);
    }

    public async Task<CombatLogResponse> SetCurrentTurn(Guid combatId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.SetCurrentTurnAsync(combatId, participantId);
        var currentTurn = await _combatService.GetCurrentTurnParticipantAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("TurnSet", new
        {
            result.CurrentRound,
            currentTurn.Id,
            currentTurn.DisplayName
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatAttackResponse> CombatAttack(Guid combatId, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus = null, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ExecuteAttackAsync(
            combatId, attackerName, weapon, targetId, attackFormula,
            attackBonus, damageFormula, damageBonus, description);

        // Update target HP on the character sheet if it's a player
        if (result.TargetHP != result.TargetMaxHP)
        {
            var participant = combat.Participants.FirstOrDefault(p => p.Id == targetId);
            if (participant?.PlayerId.HasValue == true)
            {
                var player = await _context.Players.FindAsync(participant.PlayerId.Value);
                if (player?.Character != null)
                {
                    player.Character.CurrentHP = result.TargetHP;
                    _context.Characters.Update(player.Character);
                    await _context.SaveChangesAsync();
                }
            }
        }

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAttack", new
        {
            result.Attacker,
            result.Weapon,
            result.Target,
            result.Hit,
            result.IsCritical,
            result.IsFumble,
            result.AttackRoll,
            result.AttackDice,
            result.AC,
            result.DamageDice,
            result.DamageTotal,
            result.DamageInfo,
            result.TargetHP,
            result.TargetMaxHP
        });

        return new CombatAttackResponse
        {
            Attacker = result.Attacker,
            Weapon = result.Weapon,
            Target = result.Target,
            Hit = result.Hit,
            IsCritical = result.IsCritical,
            IsFumble = result.IsFumble,
            AttackRoll = result.AttackRoll,
            AttackDice = result.AttackDice,
            AC = result.AC,
            DamageDice = result.DamageDice,
            DamageTotal = result.DamageTotal,
            DamageInfo = result.DamageInfo,
            TargetHP = result.TargetHP,
            TargetMaxHP = result.TargetMaxHP
        };
    }

    public async Task<CombatSaveThrowResponse> CombatSaveThrow(Guid combatId, string participantName,
        Guid participantId, string saveType, string saveFormula, int dc)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ExecuteSaveThrowAsync(
            combatId, participantName, participantId, saveType, saveFormula, dc);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSaveThrow", new
        {
            result.Participant,
            result.SaveType,
            result.DiceRoll,
            result.DC,
            result.Success,
            result.RolledAt
        });

        return new CombatSaveThrowResponse
        {
            Participant = result.Participant,
            SaveType = result.SaveType,
            DiceRoll = result.DiceRoll,
            DC = result.DC,
            Success = result.Success
        };
    }

    public async Task<CombatLogResponse> CombatApplyCondition(Guid combatId, Guid participantId,
        string conditionName, int? duration = null, string? description = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplyConditionAsync(
            combatId, participantId, conditionName, duration, description);

        await Clients.Group(combat.GameId.ToString()).SendAsync("ConditionApplied", new
        {
            participantId,
            conditionName,
            duration
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatRemoveCondition(Guid combatId, Guid participantId, string conditionName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RemoveConditionAsync(combatId, participantId, conditionName);

        await Clients.Group(combat.GameId.ToString()).SendAsync("ConditionRemoved", new
        {
            participantId,
            conditionName
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatDealDamage(Guid combatId, Guid participantId, int damage, string? source = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.DealDamageAsync(combatId, participantId, damage, source);

        // Update character HP if it's a player
        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant?.PlayerId.HasValue == true)
        {
            var player = await _context.Players.FindAsync(participant.PlayerId.Value);
            if (player?.Character != null)
            {
                player.Character.CurrentHP = participant.CurrentHP;
                _context.Characters.Update(player.Character);
                await _context.SaveChangesAsync();
            }
        }

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatDamage", new
        {
            ParticipantId = participantId,
            DisplayName = participant?.DisplayName,
            Damage = damage,
            HP = participant?.CurrentHP,
            MaxHP = participant?.MaxHP,
            Source = source
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatHeal(Guid combatId, Guid participantId, int amount, string? source = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.HealAsync(combatId, participantId, amount, source);

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant?.PlayerId.HasValue == true)
        {
            var player = await _context.Players.FindAsync(participant.PlayerId.Value);
            if (player?.Character != null)
            {
                player.Character.CurrentHP = participant.CurrentHP;
                _context.Characters.Update(player.Character);
                await _context.SaveChangesAsync();
            }
        }

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatHeal", new
        {
            ParticipantId = participantId,
            DisplayName = participant?.DisplayName,
            Amount = amount,
            HP = participant?.CurrentHP,
            MaxHP = participant?.MaxHP,
            Source = source,
            FlavorText = $"{participant?.DisplayName ?? "Someone"} heals {amount} HP from {source ?? "a mysterious source"}"
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatDeathSaveResponse> CombatDeathSave(Guid combatId, Guid participantId, bool success)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        var result = await _combatService.MakeDeathSaveAsync(combatId, participantId, success);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatDeathSave", new
        {
            Participant = result.Participant,
            Success = result.Success,
            Successes = result.Successes,
            Failures = result.Failures,
            IsStabilized = result.IsStabilized,
            IsDead = result.IsDead
        });

        return new CombatDeathSaveResponse
        {
            Participant = result.Participant,
            Success = result.Success,
            Successes = result.Successes,
            Failures = result.Failures,
            IsStabilized = result.IsStabilized,
            IsDead = result.IsDead
        };
    }

    public async Task<CombatLogResponse> GetCombatLog(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var log = await _combatService.GetCombatLogAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatLogUpdated", BuildCombatLogResponse(log));

        return BuildCombatLogResponse(log);
    }

    public async Task<List<CombatSummary>> GetActiveCombats(Guid gameId)
    {
        var combats = await _combatService.GetActiveCombatAsync(gameId);
        return combats.Select(c => new CombatSummary
        {
            Id = c.Id,
            Name = c.Name,
            Status = c.Status.ToString(),
            CurrentRound = c.CurrentRound,
            ParticipantCount = c.Participants.Count,
            StartedAt = c.StartedAt
        }).ToList();
    }

    // ==================== Spell Combat ====================

    public async Task<CombatSpellCastResponse> CombatCastSpell(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CastSpellAsync(
            combatId, casterName, spellName, targetId, saveFormula, saveDC,
            damageFormula, damageBonus, description);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSpellCast", new
        {
            result.Caster,
            result.SpellName,
            result.SpellLevel,
            result.Target,
            result.SaveType,
            result.SaveDC,
            result.SaveSuccess,
            result.IsCritical,
            result.DamageType,
            result.DamageTotal,
            result.DamageInfo,
            result.Effect,
            result.TargetHP,
            result.TargetMaxHP
        });

        return new CombatSpellCastResponse
        {
            Caster = result.Caster,
            SpellName = result.SpellName,
            SpellLevel = result.SpellLevel,
            Target = result.Target,
            SaveType = result.SaveType,
            SaveDC = result.SaveDC,
            SaveSuccess = result.SaveSuccess,
            IsCritical = result.IsCritical,
            DamageType = result.DamageType,
            DamageTotal = result.DamageTotal,
            DamageInfo = result.DamageInfo,
            Effect = result.Effect,
            TargetHP = result.TargetHP,
            TargetMaxHP = result.TargetMaxHP
        };
    }

    public async Task<CombatSpellCastResponse> CombatCastAreaSpell(Guid combatId, string casterName, string spellName,
        string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null, string[]? targetIds = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var targetGuids = targetIds?.Select(Guid.Parse).ToArray();
        var result = await _combatService.CastAreaSpellAsync(
            combatId, casterName, spellName, saveFormula, saveDC,
            damageFormula, damageBonus, description, targetGuids);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSpellCast", new
        {
            result.Caster,
            result.SpellName,
            result.Target,
            result.SaveType,
            result.SaveDC,
            result.SaveSuccess,
            result.DamageType,
            result.DamageTotal,
            result.DamageInfo,
            result.Effect
        });

        return new CombatSpellCastResponse
        {
            Caster = result.Caster,
            SpellName = result.SpellName,
            Target = result.Target,
            SaveType = result.SaveType,
            SaveDC = result.SaveDC,
            SaveSuccess = result.SaveSuccess,
            DamageType = result.DamageType,
            DamageTotal = result.DamageTotal,
            DamageInfo = result.DamageInfo,
            Effect = result.Effect
        };
    }

    // ==================== System-Specific ====================

    public async Task<CombatLogResponse> CombatApplySystemEffects(Guid combatId, string systemId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplySystemSpecificEffectsAsync(combatId, systemId, participantId);
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatCalculateProficiency(Guid combatId, string systemId, int level)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CalculateProficiencyBonusAsync(combatId, systemId, level);
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatCalculateSave(Guid combatId, string systemId, string saveType,
        Guid participantId, int? proficiencyBonus = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CalculateSavingThrowAsync(combatId, systemId, saveType, participantId, proficiencyBonus);
        return BuildCombatLog(result);
    }

    // ==================== Character Progression ====================

    public async Task<CombatLogResponse> CombatAddXP(Guid combatId, Guid participantId, int xpAmount, string reason)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AddXPAsync(combatId, participantId, xpAmount, reason);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatXP", new
        {
            participantId,
            xpAmount,
            reason
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLevelUpResponse> CombatLevelUp(Guid combatId, Guid participantId, int newLevel, string systemId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.LevelUpAsync(combatId, participantId, newLevel, systemId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatLevelUp", new
        {
            participantId,
            newLevel,
            systemId
        });

        return new CombatLevelUpResponse
        {
            ParticipantId = participantId,
            NewLevel = newLevel,
            SystemId = systemId
        };
    }

    public async Task<CombatLogResponse> CombatCalculateXP(Guid combatId, string systemId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CalculateXPForCombatAsync(combatId, systemId);
        return BuildCombatLog(result);
    }

    // ==================== Rest System ====================

    public async Task<CombatLogResponse> CombatStartShortRest(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.StartShortRestAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatRestStarted", new
        {
            combatId,
            restType = "Short"
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatStartLongRest(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.StartLongRestAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatRestStarted", new
        {
            combatId,
            restType = "Long"
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatEndRest(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.EndRestAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatRestEnded", new { combatId });
        return BuildCombatLog(result);
    }

    public async Task<CombatRestStatusResponse> CombatGetRestStatus(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.GetCurrentRestStatusAsync(combatId);
        return new CombatRestStatusResponse
        {
            RestType = result.RestType,
            IsInProgress = result.IsInProgress,
            RoundsRemaining = result.RoundsRemaining,
            HPRecovered = result.HPRecovered,
            Effects = result.Effects
        };
    }

    // ==================== Inventory/Equipment ====================

    public async Task<CombatLogResponse> CombatAddItem(Guid combatId, Guid participantId, string itemName,
        string itemType, int quantity = 1, JsonElement? itemStats = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AddItemToParticipantAsync(combatId, participantId, itemName, itemType, quantity, itemStats);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemAdded", new
        {
            participantId,
            itemName,
            itemType,
            quantity
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatRemoveItem(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RemoveItemFromParticipantAsync(combatId, participantId, itemName);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemRemoved", new
        {
            participantId,
            itemName
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatEquipItem(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.EquipItemAsync(combatId, participantId, itemName);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemEquipped", new
        {
            participantId,
            itemName
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatUnequipItem(Guid combatId, Guid participantId, string itemName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.UnequipItemAsync(combatId, participantId, itemName);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatItemUnequipped", new
        {
            participantId,
            itemName
        });
        return BuildCombatLog(result);
    }

    // ==================== Grid/Map ====================

    public async Task<CombatLogResponse> CombatSetGridSize(Guid combatId, int width, int height)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.SetGridSizeAsync(combatId, width, height);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatGridSet", new
        {
            combatId,
            width,
            height
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatSetPosition(Guid combatId, Guid participantId, int gridX, int gridY)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.SetParticipantPositionAsync(combatId, participantId, gridX, gridY);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatPositionSet", new
        {
            participantId,
            gridX,
            gridY
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatMoveParticipant(Guid combatId, Guid participantId, int newGridX, int newGridY)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.MoveParticipantAsync(combatId, participantId, newGridX, newGridY);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatMove", new
        {
            participantId,
            newGridX,
            newGridY
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatGridPositionResponse?> CombatGetPosition(Guid combatId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.GetParticipantPositionAsync(combatId, participantId);
        return result == null ? null : new CombatGridPositionResponse
        {
            ParticipantId = result.ParticipantId,
            GridX = result.GridX,
            GridY = result.GridY,
            DisplayName = result.DisplayName,
            MoveSpeed = result.MoveSpeed
        };
    }

    public async Task<List<CombatGridPositionResponse>> CombatGetAdjacentPositions(Guid combatId, int gridX, int gridY, int range = 1)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var positions = await _combatService.GetAdjacentPositionsAsync(combatId, gridX, gridY, range);
        return positions.Select(p => new CombatGridPositionResponse
        {
            GridX = p.GridX,
            GridY = p.GridY
        }).ToList();
    }

    // ==================== AI Combat ====================

    public async Task<CombatAISuggestionsResponse> CombatGetAISuggestions(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var suggestions = await _combatService.GetAITacticalSuggestionsAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAISuggestions", new
        {
            combatId,
            suggestions.ThreatLevel,
            suggestions.RecommendedStrategy,
            suggestions.Suggestions,
            suggestions.NPCActions,
            suggestions.Warnings
        });

        return new CombatAISuggestionsResponse
        {
            CombatId = combatId,
            ThreatLevel = suggestions.ThreatLevel,
            RecommendedStrategy = suggestions.RecommendedStrategy,
            Suggestions = suggestions.Suggestions,
            NPCActions = suggestions.NPCActions,
            Warnings = suggestions.Warnings
        };
    }

    public async Task<CombatAISuggestionsResponse> CombatGetAINPCBehavior(Guid combatId, Guid npcId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var suggestions = await _combatService.GetAINPCBehaviorAsync(combatId, npcId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAINPCBehavior", new
        {
            combatId,
            npcId,
            suggestions.NPCActions
        });

        return new CombatAISuggestionsResponse
        {
            CombatId = combatId,
            NPCActions = suggestions.NPCActions
        };
    }

    public async Task<CombatLogResponse> CombatAutoResolve(Guid combatId, string resolutionMode = "quick")
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AutoResolveCombatAsync(combatId, resolutionMode);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAutoResolved", new
        {
            combatId,
            resolutionMode
        });
        return BuildCombatLog(result);
    }

    // ==================== SAN (CoC) ====================

    public async Task<CombatLogResponse> CombatApplySANLoss(Guid combatId, Guid participantId, int sanLoss, string reason)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplySANLossAsync(combatId, participantId, sanLoss, reason);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSANLoss", new
        {
            participantId,
            sanLoss,
            reason
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatApplySANRecovery(Guid combatId, Guid participantId, int sanRecovery)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplySANRecoveryAsync(combatId, participantId, sanRecovery);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSANRecovery", new
        {
            participantId,
            sanRecovery
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatSANCheckResponse> CombatMakeSANCheck(Guid combatId, Guid participantId, int dc)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.MakeSANCheckAsync(combatId, participantId, dc);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSANCheck", new
        {
            result.Participant,
            result.CurrentSAN,
            result.Roll,
            result.DC,
            result.Success,
            result.IsCritical,
            result.SANLoss,
            result.Effect
        });

        return new CombatSANCheckResponse
        {
            Participant = result.Participant,
            CurrentSAN = result.CurrentSAN,
            Roll = result.Roll,
            DC = result.DC,
            Success = result.Success,
            IsCritical = result.IsCritical,
            SANLoss = result.SANLoss,
            Effect = result.Effect
        };
    }

    // ==================== Character Creation ====================

    public async Task<CreateCharacterResponse> CreateCharacter(Guid gameId, Guid playerId, string characterJson)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return new CreateCharacterResponse { Success = false, Error = "Authentication required." };
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return new CreateCharacterResponse { Success = false, Error = "Not an active player." };
        }

        // Verify the playerId matches the current user
        if (player.Id != playerId)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Player ID mismatch." });
            return new CreateCharacterResponse { Success = false, Error = "Player ID mismatch." };
        }

        var characterData = JsonSerializer.Deserialize<CharacterCreateInput>(characterJson);
        if (characterData == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Invalid character data." });
            return new CreateCharacterResponse { Success = false, Error = "Invalid character data." };
        }

        var character = new Character
        {
            PlayerId = playerId,
            Name = characterData.Name,
            Class = characterData.Class,
            Level = characterData.Level ?? 1,
            ProficiencyBonus = GetProficiencyBonus(characterData.Level ?? 1),
            CurrentHP = characterData.CurrentHP ?? 10,
            MaxHP = characterData.MaxHP ?? 10,
            Attributes = JsonSerializer.SerializeToElement(characterData.Attributes),
            Skills = JsonSerializer.SerializeToElement(characterData.Skills ?? new { }),
            Inventory = JsonSerializer.SerializeToElement(characterData.Inventory ?? new { }),
            Spells = JsonSerializer.SerializeToElement(new { }),
            Conditions = JsonSerializer.SerializeToElement(new { }),
            CustomFields = JsonSerializer.SerializeToElement(new { systemId = characterData.SystemId }),
            UpdatedAt = DateTime.UtcNow
        };

        _context.Characters.Add(character);
        await _context.SaveChangesAsync();

        return new CreateCharacterResponse
        {
            Success = true,
            CharacterId = character.Id,
            Name = character.Name,
            Class = character.Class,
            Level = character.Level
        };
    }

    /// <summary>
    /// Create a character as the game creator (even without being a player).
    /// </summary>
    public async Task<CreateCharacterResponse> CreateCharacterAsCreator(Guid gameId, Guid creatorUserId, string characterJson)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return new CreateCharacterResponse { Success = false, Error = "Authentication required." };
        }

        // Verify the user is the game creator
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null || game.CreatorId != uid)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the game creator can create characters in this way." });
            return new CreateCharacterResponse { Success = false, Error = "Only the game creator can do this." };
        }

        var characterData = JsonSerializer.Deserialize<CharacterCreateInput>(characterJson);
        if (characterData == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Invalid character data." });
            return new CreateCharacterResponse { Success = false, Error = "Invalid character data." };
        }

        // Create a player record for the creator if one doesn't exist
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        Guid playerId;
        if (player == null)
        {
            player = new Player
            {
                GameId = gameId,
                UserId = uid,
                CharacterName = characterData.Name ?? $"Creator {gameId:N}",
                Role = PlayerRole.Creator,
                Status = PlayerStatus.Active,
                JoinedAt = DateTime.UtcNow
            };
            _context.Players.Add(player);
            await _context.SaveChangesAsync();
            playerId = player.Id;
        }
        else
        {
            playerId = player.Id;
        }

        var character = new Character
        {
            PlayerId = playerId,
            Name = characterData.Name,
            Class = characterData.Class,
            Level = characterData.Level ?? 1,
            ProficiencyBonus = GetProficiencyBonus(characterData.Level ?? 1),
            CurrentHP = characterData.CurrentHP ?? 10,
            MaxHP = characterData.MaxHP ?? 10,
            Attributes = JsonSerializer.SerializeToElement(characterData.Attributes),
            Skills = JsonSerializer.SerializeToElement(characterData.Skills ?? new { }),
            Inventory = JsonSerializer.SerializeToElement(characterData.Inventory ?? new { }),
            Spells = JsonSerializer.SerializeToElement(new { }),
            Conditions = JsonSerializer.SerializeToElement(new { }),
            CustomFields = JsonSerializer.SerializeToElement(new { systemId = characterData.SystemId }),
            UpdatedAt = DateTime.UtcNow
        };

        _context.Characters.Add(character);
        await _context.SaveChangesAsync();

        return new CreateCharacterResponse
        {
            Success = true,
            CharacterId = character.Id,
            Name = character.Name,
            Class = character.Class,
            Level = character.Level
        };
    }

    private static int GetProficiencyBonus(int level)
    {
        if (level <= 4) return 2;
        if (level <= 8) return 3;
        if (level <= 12) return 4;
        if (level <= 16) return 5;
        return 6;
    }

    // ==================== Helpers ====================

    /// <summary>
    /// Get the SignalR connection ID for a specific player.
    /// Uses the in-memory player-to-connection mapping.
    /// </summary>
    private string? GetConnectionIdForPlayer(Guid playerId)
    {
        if (_playerConnections.TryGetValue(playerId.ToString(), out var connectionId))
        {
            return connectionId;
        }

        return null;
    }

    /// <summary>
    /// Builds a normalized whisper response object for SignalR broadcasting.
    /// </summary>
    private static object BuildWhisperResponse(Whisper w, string fromCharacter, PlayerRole fromRole) =>
        new
        {
            w.Id,
            FromPlayerId = w.FromPlayerId,
            FromCharacter = fromCharacter,
            FromRole = fromRole,
            Content = w.Content,
            Type = w.Type,
            Targets = w.Targets,
            CreatedAt = w.CreatedAt
        };

    // ==================== Combat Helpers ====================

    private static CombatLogResponse BuildCombatLog(Models.Combat combat)
    {
        var currentTurnId = combat.Participants.Count > 0
            ? combat.Participants[Math.Min(combat.CurrentTurnIndex, combat.Participants.Count - 1)].Id
            : Guid.Empty;

        return new CombatLogResponse
        {
            CombatId = combat.Id,
            Name = combat.Name,
            Status = combat.Status.ToString(),
            CurrentRound = combat.CurrentRound,
            CurrentTurnIndex = combat.CurrentTurnIndex,
            Participants = combat.Participants
                .OrderBy(p => p.Initiative)
                .ThenByDescending(p => p.InitiativeCount)
                .Select(p => new CombatParticipantResponse
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    ParticipantType = p.ParticipantType,
                    CurrentHP = p.CurrentHP,
                    MaxHP = p.MaxHP,
                    AC = p.AC,
                    Initiative = p.Initiative,
                    Conditions = JsonSerializer.Deserialize<List<ConditionEntryResponse>>(p.Conditions.ToString()) ?? new(),
                    IsCurrentTurn = p.Id == currentTurnId,
                    IsDead = p.CurrentHP <= 0
                }).ToList(),
            Events = combat.Events
                .OrderBy(e => e.CreatedAt)
                .Select(e => new CombatLogEventResponse
                {
                    Id = e.Id,
                    Round = e.Round,
                    TurnIndex = e.TurnIndex,
                    Type = e.Type.ToString(),
                    ActorName = e.ActorName,
                    TargetName = e.TargetName,
                    Content = e.Content,
                    CreatedAt = e.CreatedAt
                }).ToList()
        };
    }

    private static CombatLogResponse BuildCombatLogResponse(CombatLog log)
    {
        return new CombatLogResponse
        {
            CombatId = log.CombatId,
            Name = log.Name,
            Status = log.Status.ToString(),
            CurrentRound = log.CurrentRound,
            CurrentTurnIndex = log.CurrentTurnIndex,
            Participants = log.Participants.Select(p => new CombatParticipantResponse
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                ParticipantType = p.ParticipantType,
                CurrentHP = p.CurrentHP,
                MaxHP = p.MaxHP,
                AC = p.AC,
                Initiative = p.Initiative,
                Conditions = p.Conditions.Select(c => new ConditionEntryResponse
                {
                    Name = c.Name,
                    Duration = c.Duration,
                    Description = c.Description
                }).ToList(),
                IsCurrentTurn = p.IsCurrentTurn,
                IsDead = p.IsDead
            }).ToList(),
            Events = log.Events.Select(e => new CombatLogEventResponse
            {
                Id = e.Id,
                Round = e.Round,
                TurnIndex = e.TurnIndex,
                Type = e.Type.ToString(),
                ActorName = e.ActorName,
                TargetName = e.TargetName,
                Content = e.Content,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    private static CombatParticipantResponse BuildParticipantResponse(Models.CombatParticipant p)
    {
        return new CombatParticipantResponse
        {
            Id = p.Id,
            DisplayName = p.DisplayName,
            ParticipantType = p.ParticipantType,
            CurrentHP = p.CurrentHP,
            MaxHP = p.MaxHP,
            AC = p.AC,
            Initiative = p.Initiative,
            Conditions = JsonSerializer.Deserialize<List<ConditionEntryResponse>>(p.Conditions.ToString()) ?? new(),
            IsCurrentTurn = false,
            IsDead = p.CurrentHP <= 0
        };
    }

    // ==================== GM Tool Calls ====================

    /// <summary>
    /// Confirm a tool call that requires user input (e.g., ask a player to roll).
    /// The GM approves, and the tool proceeds.
    /// </summary>
    public async Task<ToolCallConfirmationResponse> ConfirmToolCall(Guid toolCallId, bool approved)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
            throw new ForbiddenException("Only the GM can confirm tool calls.");

        var toolCall = await _context.GMToolCalls.FindAsync(toolCallId);
        if (toolCall == null)
            throw new KeyNotFoundException($"Tool call {toolCallId} not found.");

        if (toolCall.Status != ToolCallStatus.WaitingConfirmation)
            throw new InvalidOperationException($"Tool call is not waiting confirmation (status: {toolCall.Status}).");

        if (approved)
        {
            toolCall.Status = ToolCallStatus.Confirmed;
            toolCall.ConfirmedBy = gmPlayer.Id;
            toolCall.ConfirmedAt = DateTime.UtcNow;

            // Re-execute the tool
            var result = await _toolRegistry.ExecuteToolAsync(toolCall.GameId, toolCall.SessionId ?? Guid.Empty,
                toolCall.ToolName, toolCall.Arguments!);

            toolCall.Status = result.Success ? ToolCallStatus.Completed : ToolCallStatus.Failed;
            toolCall.Result = result.Output;
            toolCall.OutputMessage = result.OutputMessage;
            toolCall.Error = result.Error;
            toolCall.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify all players in the game
            await Clients.Group(toolCall.GameId.ToString()).SendAsync("ToolCallConfirmed",
                new { toolCall.Id, toolCall.ToolName, toolCall.OutputMessage, toolCall.Result, approved = true });
        }
        else
        {
            toolCall.Status = ToolCallStatus.Cancelled;
            toolCall.Error = "Cancelled by GM";
            toolCall.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await Clients.Group(toolCall.GameId.ToString()).SendAsync("ToolCallConfirmed",
                new { toolCall.Id, toolCall.ToolName, approved = false });
        }

        return new ToolCallConfirmationResponse
        {
            Id = toolCall.Id,
            ToolName = toolCall.ToolName,
            Approved = approved,
            OutputMessage = toolCall.OutputMessage,
            Status = toolCall.Status
        };
    }

    /// <summary>
    /// Player confirms they will roll (for player-initiated rolls).
    /// Returns the roll formula so the player can roll in their UI.
    /// </summary>
    public async Task<PlayerRollConfirmationResponse> ConfirmPlayerRoll(Guid toolCallId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
            throw new InvalidOperationException("Player not active in any game.");

        var toolCall = await _context.GMToolCalls.FindAsync(toolCallId);
        if (toolCall == null)
            throw new KeyNotFoundException($"Tool call {toolCallId} not found.");

        if (toolCall.Status != ToolCallStatus.WaitingConfirmation)
            throw new InvalidOperationException($"Tool call is not waiting confirmation.");

        // Check if this player is in the target list
        var args = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Arguments ?? "{}") ?? new();
        var targetPlayerIds = new List<Guid>();
        if (args.TryGetValue("playerIds", out var pids) && pids is System.Collections.IEnumerable pidsList)
        {
            foreach (var pid in pidsList)
            {
                if (pid is string ps && Guid.TryParse(ps, out var guid))
                    targetPlayerIds.Add(guid);
            }
        }
        else if (args.TryGetValue("playerId", out var pidObj) && pidObj is string ps2 && Guid.TryParse(ps2, out var guid2))
        {
            targetPlayerIds.Add(guid2);
        }

        // If no specific targets, anyone can confirm
        if (targetPlayerIds.Count > 0 && !targetPlayerIds.Contains(player.Id))
            throw new ForbiddenException("This roll was not assigned to you.");

        // Mark as confirmed
        toolCall.Status = ToolCallStatus.Confirmed;
        toolCall.ConfirmedBy = player.Id;
        toolCall.ConfirmedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Send the roll details to the player
        var result = new PlayerRollConfirmationResponse
        {
            ToolCallId = toolCall.Id,
            Approved = true,
            Skill = args.GetValueOrDefault("skill")?.ToString() ?? "",
            Formula = args.GetValueOrDefault("formula")?.ToString() ?? "1d20",
            DC = args.GetValueOrDefault("dc") is int dc ? dc : 15,
            Context = args.GetValueOrDefault("context")?.ToString() ?? "",
            Optional = args.GetValueOrDefault("optional") is bool o && o
        };

        // Notify the player with roll details
        await Clients.Caller.SendAsync("PlayerRollRequested", new
        {
            toolCall.Id,
            result.Skill,
            result.Formula,
            result.DC,
            result.Context,
            result.Optional,
            result.Approved
        });

        // Notify others that this player confirmed
        await Clients.Group(toolCall.GameId.ToString()).SendAsync("PlayerRollConfirmed", new
        {
            toolCall.Id,
            ToolName = toolCall.ToolName,
            PlayerId = player.Id,
            PlayerName = player.CharacterName,
            Skill = result.Skill,
            Formula = result.Formula,
            DC = result.DC
        });

        return new PlayerRollConfirmationResponse
        {
            ToolCallId = toolCall.Id,
            Approved = true,
            Skill = result.Skill,
            Formula = result.Formula,
            DC = result.DC,
            Context = result.Context,
            Optional = result.Optional
        };
    }

    /// <summary>
    /// Player declines a roll request.
    /// </summary>
    public async Task DeclinePlayerRoll(Guid toolCallId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
            throw new InvalidOperationException("Player not active in any game.");

        var toolCall = await _context.GMToolCalls.FindAsync(toolCallId);
        if (toolCall == null)
            throw new KeyNotFoundException($"Tool call {toolCallId} not found.");

        if (toolCall.Status != ToolCallStatus.WaitingConfirmation)
            throw new InvalidOperationException($"Tool call is not waiting confirmation.");

        toolCall.Status = ToolCallStatus.Cancelled;
        toolCall.Error = "Declined by player";
        toolCall.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Notify the game
        await Clients.Group(toolCall.GameId.ToString()).SendAsync("PlayerRollDeclined", new
        {
            toolCall.Id,
            PlayerId = player.Id,
            PlayerName = player.CharacterName,
            Skill = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Arguments ?? "{}")?.GetValueOrDefault("skill")?.ToString() ?? ""
        });

        // Notify the GM
        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == toolCall.GameId && p.Role == PlayerRole.Creator && p.Status == PlayerStatus.Active);

        if (gmPlayer != null)
        {
            var gmConnectionId = GetConnectionIdForPlayer(gmPlayer.Id);
            if (gmConnectionId != null)
            {
                await Clients.Client(gmConnectionId).SendAsync("PlayerRollDeclined", new
                {
                    toolCall.Id,
                    PlayerId = player.Id,
                    PlayerName = player.CharacterName,
                    Skill = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Arguments ?? "{}")?.GetValueOrDefault("skill")?.ToString() ?? ""
                });
            }
        }
    }

    /// <summary>
    /// Get pending tool calls for a game.
    /// </summary>
    public async Task<List<ToolCallInfo>> GetPendingToolCalls(Guid gameId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException("Game not found.");

        var hasAccess = game.CreatorId == uid || game.Players.Any(p => p.UserId == uid);
        if (!hasAccess)
            throw new ForbiddenException("You don't have access to this game.");

        var pendingCalls = await _context.GMToolCalls
            .Where(t => t.GameId == gameId &&
                        (t.Status == ToolCallStatus.WaitingConfirmation || t.Status == ToolCallStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync();

        return pendingCalls.Select(t => new ToolCallInfo
        {
            Id = t.Id,
            ToolName = t.ToolName,
            Status = t.Status,
            Arguments = t.Arguments,
            OutputMessage = t.OutputMessage,
            CreatedAt = t.CreatedAt,
            RequiresConfirmation = t.RequiresConfirmation
        }).ToList();
    }

    /// <summary>
    /// Broadcast a tool call notification to all players in the game.
    /// Called when the GM agent requests a tool that requires confirmation.
    /// </summary>
    public async Task BroadcastToolCallNotification(Guid gameId, string toolCallId, string toolName, string outputMessage)
    {
        await Clients.Group(gameId.ToString()).SendAsync("ToolCallNotification", new
        {
            toolCallId,
            toolName,
            outputMessage,
            requiresConfirmation = true,
            timestamp = DateTime.UtcNow
        });
    }

    // ==================== Helper Methods ====================

    private ToolCallConfirmationResponse BuildToolCallConfirmationResponse(GMToolCall toolCall, bool approved)
    {
        return new ToolCallConfirmationResponse
        {
            Id = toolCall.Id,
            ToolName = toolCall.ToolName,
            Approved = approved,
            OutputMessage = toolCall.OutputMessage,
            Status = toolCall.Status
        };
    }

    // ==================== Flavor Text Helpers ====================
    // Short, evocative text for immediate broadcast alongside mechanical results.
    // The GM agent adds deeper narrative separately via AgentCalls.

    private string GenerateFlavorText(string eventType, params object?[] args)
    {
        return eventType switch
        {
            "dice" => GenerateDiceFlavor((string?)args[0], (int?)args[1]),
            "skillcheck" => GenerateSkillCheckFlavor(
                (string?)args[1], (int?)args[2], (int?)args[3], (bool?)args[4]),
            "attack" => GenerateAttackFlavor(
                (string?)args[1], (string?)args[2], (bool?)args[3], (int?)args[4]),
            _ => ""
        };
    }

    private string GenerateDiceFlavor(string? formula, int? total)
    {
        if (string.IsNullOrEmpty(formula) || total == null) return "";
        var rolls = formula.Split('+').Select(f => f.Trim()).ToArray();
        var lastRoll = rolls.LastOrDefault();
        return lastRoll?.Contains("d") == true
            ? $"Rolling {lastRoll}... total: {total}"
            : $"Roll: {total}";
    }

    private string GenerateSkillCheckFlavor(string? skill, int? total, int? dc, bool? success)
    {
        if (string.IsNullOrEmpty(skill) || total == null) return "";
        var result = success == true ? "succeeds" : "fails";
        var dcText = dc != null ? $" (DC {dc})" : "";
        return $"{skill} check: {total}{dcText} — {result}";
    }

    private string GenerateAttackFlavor(string? weapon, string? target, bool? hit, int? damage)
    {
        if (string.IsNullOrEmpty(weapon)) return "";
        var targetText = !string.IsNullOrEmpty(target) ? $" vs {target}" : "";
        return hit == true
            ? $"{weapon}{targetText} — hit! ({damage} damage)"
            : $"{weapon}{targetText} — miss!";
    }

    private string GenerateCombatAttackFlavor(CombatAttackResponse result)
    {
        if (string.IsNullOrEmpty(result.Weapon)) return "";
        var targetText = !string.IsNullOrEmpty(result.Target) ? $" on {result.Target}" : "";
        if (result.IsCritical == true)
            return $"{result.Attacker} {result.Weapon}{targetText} — CRITICAL! ({result.DamageTotal} damage)";
        if (result.IsFumble == true)
            return $"{result.Attacker} {result.Weapon}{targetText} — fumble!";
        if (result.Hit == true)
            return $"{result.Attacker} {result.Weapon}{targetText} — hit! ({result.DamageTotal} damage)";
        return $"{result.Attacker} {result.Weapon}{targetText} — miss!";
    }

    private string GenerateSaveThrowFlavor(string participant, string saveType, bool success, int dc)
    {
        var result = success ? "succeeds" : "fails";
        return $"{participant} {saveType} save (DC {dc}): {result}";
    }
}

// ==================== Response Types ====================

public class ToolCallConfirmationResponse
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public bool Approved { get; set; }
    public string? OutputMessage { get; set; }
    public ToolCallStatus Status { get; set; }
}

public class PlayerRollConfirmationResponse
{
    public Guid ToolCallId { get; set; }
    public bool Approved { get; set; }
    public string Skill { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public int DC { get; set; }
    public string Context { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

public class ToolCallInfo
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public ToolCallStatus Status { get; set; }
    public string? Arguments { get; set; }
    public string? OutputMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool RequiresConfirmation { get; set; }
}

// ============= Response DTOs =============

public class WhisperResponse
{
    public Guid Id { get; set; }
    public Guid FromPlayerId { get; set; }
    public string FromCharacter { get; set; } = string.Empty;
    public PlayerRole FromRole { get; set; }
    public string Content { get; set; } = string.Empty;
    public Adnd.Server.Models.WhisperType Type { get; set; }
    public string Targets { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AgentCallResponse
{
    public Guid Id { get; set; }
    public AgentType FromAgent { get; set; }
    public AgentType ToAgent { get; set; }
    public AgentAction Action { get; set; }
    public AgentCallStatus Status { get; set; }
    public string? Output { get; set; }
    public string? OutputMessage { get; set; }
    public int DurationMs { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// ============= Combat Response DTOs =============

public class CombatLogResponse
{
    public Guid CombatId { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = "Active";
    public int CurrentRound { get; set; }
    public int CurrentTurnIndex { get; set; }
    public List<CombatParticipantResponse> Participants { get; set; } = new();
    public List<CombatLogEventResponse> Events { get; set; } = new();
}

public class CombatParticipantResponse
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int AC { get; set; }
    public int Initiative { get; set; }
    public List<ConditionEntryResponse> Conditions { get; set; } = new();
    public bool IsCurrentTurn { get; set; }
    public bool IsDead { get; set; }
}

public class ConditionEntryResponse
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string? Description { get; set; }
}

public class CombatLogEventResponse
{
    public Guid Id { get; set; }
    public int Round { get; set; }
    public int TurnIndex { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CombatAttackResponse
{
    public string Attacker { get; set; } = string.Empty;
    public string Weapon { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Hit { get; set; }
    public bool IsCritical { get; set; }
    public bool IsFumble { get; set; }
    public int AttackRoll { get; set; }
    public int AttackDice { get; set; }
    public int AC { get; set; }
    public string DamageDice { get; set; } = string.Empty;
    public int DamageTotal { get; set; }
    public string DamageInfo { get; set; } = string.Empty;
    public int TargetHP { get; set; }
    public int TargetMaxHP { get; set; }
}

public class CombatSaveThrowResponse
{
    public string Participant { get; set; } = string.Empty;
    public string SaveType { get; set; } = string.Empty;
    public int DiceRoll { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
}

public class CombatDeathSaveResponse
{
    public string Participant { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public bool IsStabilized { get; set; }
    public bool IsDead { get; set; }
}

public class CombatSummary
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = "Active";
    public int CurrentRound { get; set; }
    public int ParticipantCount { get; set; }
    public DateTime StartedAt { get; set; }
}

// ============= Spell Response DTOs =============

public class CombatSpellCastResponse
{
    public string Caster { get; set; } = string.Empty;
    public string SpellName { get; set; } = string.Empty;
    public string SpellLevel { get; set; } = "0";
    public string Target { get; set; } = string.Empty;
    public string SaveType { get; set; } = string.Empty;
    public int SaveDC { get; set; }
    public bool SaveSuccess { get; set; }
    public bool IsCritical { get; set; }
    public string DamageType { get; set; } = string.Empty;
    public int DamageTotal { get; set; }
    public string DamageInfo { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public int? TargetHP { get; set; }
    public int? TargetMaxHP { get; set; }
}

// ============= Progression Response DTOs =============

public class CombatLevelUpResponse
{
    public Guid ParticipantId { get; set; }
    public int NewLevel { get; set; }
    public string SystemId { get; set; } = string.Empty;
}

// ============= Rest Response DTOs =============

public class CombatRestStatusResponse
{
    public string RestType { get; set; } = string.Empty;
    public bool IsInProgress { get; set; }
    public int RoundsRemaining { get; set; }
    public int HPRecovered { get; set; }
    public List<string> Effects { get; set; } = new();
}

// ============= Grid Response DTOs =============

public class CombatGridPositionResponse
{
    public Guid ParticipantId { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int MoveSpeed { get; set; }
}

// ============= AI Combat Response DTOs =============

public class CombatAISuggestionsResponse
{
    public Guid CombatId { get; set; }
    public string ThreatLevel { get; set; } = "Low";
    public string RecommendedStrategy { get; set; } = string.Empty;
    public List<Models.AITacticalAction> Suggestions { get; set; } = new();
    public List<Models.AINPCAction> NPCActions { get; set; } = new();
    public List<Models.AICombatWarning> Warnings { get; set; } = new();
}

// ============= SAN Response DTOs =============

public class CombatSANCheckResponse
{
    public string Participant { get; set; } = string.Empty;
    public int CurrentSAN { get; set; }
    public int Roll { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
    public bool IsCritical { get; set; }
    public int SANLoss { get; set; }
    public string Effect { get; set; } = string.Empty;
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

// ============= Character Creation DTOs =============

public class CreateCharacterResponse
{
    public bool Success { get; set; }
    public Guid? CharacterId { get; set; }
    public string? Name { get; set; }
    public string? Class { get; set; }
    public int? Level { get; set; }
    public string? Error { get; set; }
}

public class CharacterCreateInput
{
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int? Level { get; set; }
    public int? CurrentHP { get; set; }
    public int? MaxHP { get; set; }
    public object? Attributes { get; set; }
    public object? Skills { get; set; }
    public object? Inventory { get; set; }
    public string? SystemId { get; set; }
}
