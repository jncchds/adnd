using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Hubs;
using Adnd.Server.Controllers;

namespace Adnd.Server.Services;

/// <summary>
/// Service for session CRUD operations.
/// </summary>
public interface ISessionManagementService
{
    Task<List<object>> GetSessionsAsync(Guid gameId, AppDbContext context);
    Task<(GameSession Session, object Response)> CreateSessionAsync(Guid gameId, Guid creatorId, CreateSessionRequest request);
    Task CloseSessionAsync(Guid gameId, Guid sessionId);
}

public class SessionManagementService : ISessionManagementService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SessionManagementService> _logger;

    public SessionManagementService(AppDbContext context, ILogger<SessionManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<object>> GetSessionsAsync(Guid gameId, AppDbContext context)
    {
        return (await context.GameSessions
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.Description,
                s.StartedAt,
                s.EndedAt,
                MessageCount = context.Messages.Count(m => m.SessionId == s.Id)
            })
            .ToListAsync()).Cast<object>().ToList();
    }

    public async Task<(GameSession Session, object Response)> CreateSessionAsync(Guid gameId, Guid creatorId, CreateSessionRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException("Game not found.");
        if (game.CreatorId != creatorId)
            throw new UnauthorizedAccessException("Only creator can create sessions.");

        var session = new GameSession
        {
            GameId = gameId,
            Title = request.Title,
            Description = request.Description,
            StartedAt = DateTime.UtcNow
        };

        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync();

        return (session, new { session.Id, session.Title, session.StartedAt });
    }

    public async Task CloseSessionAsync(Guid gameId, Guid sessionId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException("Game not found.");

        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null || session.GameId != gameId)
            throw new KeyNotFoundException("Session not found.");

        session.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Service for player management: join, leave, promote.
/// </summary>
public interface IPlayerManagementService
{
    Task<List<object>> GetPlayersAsync(Guid gameId, AppDbContext context);
    Task PromotePlayerAsync(Guid gameId, Guid creatorId, Guid playerId, string role);
    Task<(Player Player, string Message)> JoinGameAsync(Guid gameId, string userId, int existingPlayerCount);
    Task LeaveGameAsync(Guid gameId, string userId);
    Task<(Player Player, string Message)> JoinByCodeAsync(string code, string userId);
}

public class PlayerManagementService : IPlayerManagementService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<PlayerManagementService> _logger;

    public PlayerManagementService(AppDbContext context,
        IHubContext<GameHub> hubContext,
        ILogger<PlayerManagementService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<List<object>> GetPlayersAsync(Guid gameId, AppDbContext context)
    {
        return (await context.Players
            .Where(p => p.GameId == gameId)
            .Select(p => new
            {
                p.Id,
                p.CharacterName,
                p.Role,
                p.Status,
                p.JoinedAt,
                p.Character,
                UserName = p.User != null ? (p.User.DisplayName ?? p.User.Email) : "Unknown",
                UserEmail = p.User != null ? p.User.Email : null
            })
            .ToListAsync()).Cast<object>().ToList();
    }

    public async Task PromotePlayerAsync(Guid gameId, Guid creatorId, Guid playerId, string role)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException("Game not found.");
        if (game.CreatorId != creatorId)
            throw new UnauthorizedAccessException("Only creator can promote players.");

        var player = await _context.Players.FirstOrDefaultAsync(p => p.GameId == gameId && p.Id == playerId);
        if (player == null)
            throw new KeyNotFoundException("Player not found.");

        if (!Enum.TryParse(role, true, out PlayerRole parsedRole))
            throw new ArgumentException($"Invalid role. Valid roles: Creator, Player, Spectator, Observer");

        player.Role = parsedRole;
        await _context.SaveChangesAsync();
    }

    public async Task<(Player Player, string Message)> JoinGameAsync(Guid gameId, string userId, int existingPlayerCount)
    {
        if (!Guid.TryParse(userId, out var uid))
            throw new InvalidOperationException("Invalid user ID.");

        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException("Game not found.");

        if (game.Players.Any(p => p.UserId == uid))
            throw new InvalidOperationException("You are already a player in this game.");

        var player = new Player
        {
            GameId = gameId,
            UserId = uid,
            CharacterName = $"Player {existingPlayerCount + 1}",
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active,
            JoinedAt = DateTime.UtcNow
        };

        game.Players.Add(player);
        await _context.SaveChangesAsync();

        return (player, "Joined game successfully.");
    }

    public async Task LeaveGameAsync(Guid gameId, string userId)
    {
        if (!Guid.TryParse(userId, out var uid))
            throw new InvalidOperationException("Invalid user ID.");

        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException("Game not found.");

        var player = game.Players.FirstOrDefault(p => p.UserId == uid);
        if (player == null)
            throw new InvalidOperationException("You are not a player in this game.");

        // If creator is leaving, they must transfer ownership first or delete the game
        if (game.CreatorId == uid)
        {
            var otherPlayers = game.Players.Where(p => p.Id != player.Id).ToList();
            if (otherPlayers.Any(p => p.Role == PlayerRole.Player))
            {
                // Promote the first active player to creator
                var newCreator = otherPlayers.First(p => p.Role == PlayerRole.Player);
                game.CreatorId = newCreator.UserId;
                player.Role = PlayerRole.Player;
                _logger.LogInformation("Creator {UserId} left game {GameId} — promoted player {PlayerId} to creator",
                    uid, gameId, newCreator.Id);
            }
            else
            {
                throw new InvalidOperationException("Cannot leave as creator while other players are present. Transfer ownership first or delete the game.");
            }
        }

        player.Status = PlayerStatus.Left;
        player.LeftAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<(Player Player, string Message)> JoinByCodeAsync(string code, string userId)
    {
        code = code.TrimStart('/');

        if (!Guid.TryParse(userId, out var uid))
            throw new InvalidOperationException("Invalid user ID.");

        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.InviteCode != null && g.InviteCode!.ToLower() == code.ToLower());

        if (game == null)
            throw new KeyNotFoundException("Game not found with this invite code.");

        if (game.Status != GameStatus.Active)
            throw new InvalidOperationException("This game is not currently active.");

        if (game.Players.Any(p => p.UserId == uid))
            throw new InvalidOperationException("You are already a player in this game.");

        var player = new Player
        {
            GameId = game.Id,
            UserId = uid,
            CharacterName = $"Player {game.Players.Count + 1}",
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active,
            JoinedAt = DateTime.UtcNow
        };

        game.Players.Add(player);
        await _context.SaveChangesAsync();

        // Broadcast player joined to the game group
        await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("PlayerJoined", new
        {
            ConnectionId = "join-by-code",
            UserId = uid,
            PlayerId = player.Id,
            CharacterName = player.CharacterName,
            Role = player.Role.ToString(),
            Message = $"{player.CharacterName} joined the game"
        });

        return (player, $"Joined game successfully. Game: {game.Id}, Invite: {game.InviteCode}");
    }
}
