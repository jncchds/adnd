using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
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
    private readonly ILogger<GameHub> _logger;

    public GameHub(AppDbContext context, IGameEngine gameEngine, IAgentBus agentBus,
        IWhisperService whisperService, ILogger<GameHub> logger)
    {
        _context = context;
        _gameEngine = gameEngine;
        _agentBus = agentBus;
        _whisperService = whisperService;
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

    public async Task SendMessage(Guid sessionId, string content, MessageType type, JsonElement? metadata = null)
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
            Type = type,
            Metadata = metadata ?? default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        await Clients.Group(session.GameId.ToString()).SendAsync("NewMessage", new
        {
            message.Id,
            message.SessionId,
            message.PlayerId,
            message.Content,
            message.Type,
            message.Metadata,
            WhisperFromId = (Guid?)null,
            WhisperTarget = (string?)null,
            message.CreatedAt
        });
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

        WhisperType whisperType;
        if (targets == "all")
        {
            whisperType = player.Role == PlayerRole.GM ? WhisperType.GMToAll : WhisperType.PlayerToPlayer;
        }
        else if (targets.Contains("player:", StringComparison.OrdinalIgnoreCase))
        {
            var isGMToPlayer = player.Role == PlayerRole.GM;
            whisperType = isGMToPlayer ? WhisperType.GMToPlayer : WhisperType.PlayerToPlayer;
        }
        else
        {
            whisperType = player.Role == PlayerRole.GM ? WhisperType.GMToGroup : WhisperType.PlayerToPlayer;
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

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.GM)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the GM can send GM whispers." });
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
            new List<Guid> { targetPlayerId }, WhisperType.GMToPlayer, content);

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

        var isGM = player.Role == PlayerRole.GM;
        var whispers = await _whisperService.GetWhispersForPlayerAsync(gameId, player.Id, isGM, limit);

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

        await Clients.Group(sessionId.ToString()).SendAsync("DiceRollResult", new
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
            Timestamp = result.RolledAt
        });
    }

    // ==================== Skill Checks ====================

    public async Task SkillCheck(Guid sessionId, string skill, Guid? playerId = null, int? dc = null)
    {
        var result = await _gameEngine.SkillCheckAsync(sessionId, skill, playerId, dc);

        await Clients.Group(sessionId.ToString()).SendAsync("SkillCheckResult", new
        {
            Skill = result.Skill,
            DiceRoll = result.DiceRoll,
            Modifier = result.Modifier,
            Total = result.Total,
            DC = result.DC,
            Success = result.Success,
            RolledAt = result.RolledAt
        });
    }

    // ==================== Attacks ====================

    public async Task Attack(Guid sessionId, string weapon, string targetName, Guid? playerId = null)
    {
        var result = await _gameEngine.AttackAsync(sessionId, weapon, targetName, playerId);

        await Clients.Group(sessionId.ToString()).SendAsync("AttackResult", new
        {
            Weapon = result.Weapon,
            Target = result.Target,
            Hit = result.Hit,
            AttackRoll = result.AttackRoll,
            AC = result.AC,
            DamageDice = result.DamageDice,
            DamageTotal = result.DamageTotal,
            RolledAt = result.RolledAt
        });
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
}

// ============= Response DTOs =============

public class WhisperResponse
{
    public Guid Id { get; set; }
    public Guid FromPlayerId { get; set; }
    public string FromCharacter { get; set; } = string.Empty;
    public PlayerRole FromRole { get; set; }
    public string Content { get; set; } = string.Empty;
    public WhisperType Type { get; set; }
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

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
