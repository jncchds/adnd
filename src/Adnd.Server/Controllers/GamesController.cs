using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Data;
using Adnd.Server.Events;
using MediatR;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IUserIdProvider _userIdProvider;
    private readonly IGameAuthorizationService _authService;
    private readonly IAgentBus _agentBus;
    private readonly IMediator _mediator;
    private readonly ILogger<GamesController> _logger;

    public GamesController(
        AppDbContext context,
        IUserIdProvider userIdProvider,
        IGameAuthorizationService authService,
        IAgentBus agentBus,
        IMediator mediator,
        ILogger<GamesController> logger)
    {
        _context = context;
        _userIdProvider = userIdProvider;
        _authService = authService;
        _agentBus = agentBus;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetGames()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var games = await _context.Games
            .Where(g => g.CreatorId == id || g.Players.Any(p => p.UserId == id))
            .Select(g => new GameResponse
            {
                Id = g.Id,
                CreatorId = g.CreatorId,
                CreatorName = g.Creator != null ? (g.Creator.DisplayName ?? g.Creator.Email) : "Unknown",
                Name = g.Name,
                SystemId = g.SystemId,
                SystemVersion = g.SystemVersion,
                Status = g.Status,
                GMStatus = g.GMStatus,
                CreatedAt = g.CreatedAt,
                InviteCode = g.InviteCode,
                LLMPresetId = g.LLMPresetId,
                LLMPresetName = g.LLMPreset != null ? g.LLMPreset.Name : null
            })
            .ToListAsync();

        return Ok(games);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGame(Guid id)
    {
        var game = await _context.Games
            .Include(g => g.Creator)
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
        {
            return NotFound(new { error = "Game not found." });
        }

        // Check if user has access
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null && Guid.TryParse(userId, out var uid))
        {
            var hasAccess = game.CreatorId == uid || game.Players.Any(p => p.UserId == uid);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        return Ok(new GameResponse
        {
            Id = game.Id,
            CreatorId = game.CreatorId,
            CreatorName = game.Creator!.DisplayName ?? game.Creator.Email,
            Name = game.Name,
            SystemId = game.SystemId,
            SystemVersion = game.SystemVersion,
            Status = game.Status,
            GMStatus = game.GMStatus,
            CreatedAt = game.CreatedAt,
            InviteCode = game.InviteCode,
            PlotSeed = game.PlotSeed,
            GameParameters = game.GameParameters,
            GameState = game.GameState,
            LLMPresetId = game.LLMPresetId,
            LLMPresetName = game.LLMPreset?.Name
        });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateGame([FromBody] CreateGameRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var game = new Game
        {
            CreatorId = id,
            Name = request.Name,
            SystemId = request.SystemId,
            SystemVersion = request.SystemVersion,
            CustomSystemJson = request.CustomSystemJson,
            PlotSeed = request.PlotSeed,
            GameParameters = request.GameParameters,
            LLMPresetId = request.LLMPresetId,
            GMStatus = GMStatus.Idle,
            Status = GameStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        // Generate invite code
        game.InviteCode = GenerateInviteCode();

        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Publish game created event
        await _mediator.Publish(new GameCreated(game.Id, id, request.SystemId, request.LLMPresetId));

        return Ok(new GameResponse
        {
            Id = game.Id,
            CreatorId = game.CreatorId,
            CreatorName = (await _context.Users.FindAsync(id))!.DisplayName ?? (await _context.Users.FindAsync(id))!.Email,
            Name = game.Name,
            SystemId = game.SystemId,
            SystemVersion = game.SystemVersion,
            Status = game.Status,
            GMStatus = game.GMStatus,
            CreatedAt = game.CreatedAt,
            InviteCode = game.InviteCode,
            PlotSeed = game.PlotSeed,
            GameParameters = game.GameParameters,
            GameState = game.GameState,
            LLMPresetId = game.LLMPresetId,
            LLMPresetName = game.LLMPreset?.Name
        });
    }

    [HttpPost("{id}/invite")]
    [Authorize]
    public async Task<IActionResult> GenerateInvite(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var uid))
        {
            return Unauthorized();
        }

        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null || game.CreatorId != uid)
        {
            return Forbid();
        }

        game.InviteCode = GenerateInviteCode();
        await _context.SaveChangesAsync();

        return Ok(new InviteResponse
        {
            InviteCode = game.InviteCode!,
            InviteUrl = $"/join/{game.InviteCode}"
        });
    }

    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<IActionResult> JoinGame(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var uid))
        {
            return Unauthorized();
        }

        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
        {
            return NotFound(new { error = "Game not found." });
        }

        // Check if already a player
        if (game.Players.Any(p => p.UserId == uid))
        {
            return BadRequest(new { error = "You are already a player in this game." });
        }

        var player = new Player
        {
            GameId = id,
            UserId = uid,
            CharacterName = $"Player {game.Players.Count + 1}",
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active,
            JoinedAt = DateTime.UtcNow
        };

        game.Players.Add(player);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Joined game successfully." });
    }

    [HttpPost("{id}/leave")]
    [Authorize]
    public async Task<IActionResult> LeaveGame(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var uid))
        {
            return Unauthorized();
        }

        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
        {
            return NotFound(new { error = "Game not found." });
        }

        var player = game.Players.FirstOrDefault(p => p.UserId == uid);
        if (player == null)
        {
            return BadRequest(new { error = "You are not a player in this game." });
        }

        player.Status = PlayerStatus.Left;
        player.LeftAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Left game successfully." });
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteGame(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var uid))
        {
            return Unauthorized();
        }

        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null || game.CreatorId != uid)
        {
            return Forbid();
        }

        _context.Games.Remove(game);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Game deleted successfully." });
    }

    // ==================== Session Management ====================

    [HttpGet("{id}/sessions")]
    public async Task<IActionResult> GetSessions(Guid id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });

        if (!await _authService.HasAccessAsync(_context, id, _userIdProvider.GetCurrentUserId())) return Forbid();

        var sessions = await _context.GameSessions
            .Where(s => s.GameId == id)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.Description,
                s.StartedAt,
                s.EndedAt,
                MessageCount = _context.Messages.Count(m => m.SessionId == s.Id)
            })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpPost("{id}/sessions")]
    public async Task<IActionResult> CreateSession(Guid id, [FromBody] CreateSessionRequest request)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var session = new GameSession
        {
            GameId = id,
            Title = request.Title,
            Description = request.Description,
            StartedAt = DateTime.UtcNow
        };

        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync();

        return Ok(new { session.Id, session.Title, session.StartedAt });
    }

    [HttpPost("{id}/sessions/{sessionId}/close")]
    public async Task<IActionResult> CloseSession(Guid id, Guid sessionId)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null || session.GameId != id)
            return NotFound(new { error = "Session not found." });

        session.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { session.Id, session.EndedAt });
    }

    [HttpGet("{id}/players")]
    public async Task<IActionResult> GetPlayers(Guid id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, id, _userIdProvider.GetCurrentUserId())) return Forbid();

        var players = await _context.Players
            .Where(p => p.GameId == id)
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
            .ToListAsync();

        return Ok(players);
    }

    [HttpPost("{id}/players/{playerId}/promote")]
    public async Task<IActionResult> PromotePlayer(Guid id, Guid playerId, [FromBody] string role)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        var player = await _context.Players.FirstOrDefaultAsync(p => p.GameId == id && p.Id == playerId);
        if (player == null) return NotFound(new { error = "Player not found." });

        if (!Enum.TryParse(role, true, out PlayerRole parsedRole))
            return BadRequest(new { error = $"Invalid role. Use: Creator, Player, Spectator" });

        player.Role = parsedRole;
        await _context.SaveChangesAsync();

        return Ok(new { player.Id, player.Role });
    }

    private string GenerateInviteCode()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = Random.Shared;
        var code = new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        // Ensure uniqueness
        if (_context.Games.Any(g => g.InviteCode == code))
        {
            return GenerateInviteCode();
        }

        return code;
    }

}

public class CreateSessionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
