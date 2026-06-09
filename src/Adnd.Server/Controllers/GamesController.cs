using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using MediatR;

namespace Adnd.Server.Controllers;

/// <summary>
/// Game management endpoints — create, join, list, and manage TTRPG games and sessions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly IGameManagementService _gameService;
    private readonly ISessionManagementService _sessionService;
    private readonly IPlayerManagementService _playerService;
    private readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    private readonly IGameAuthorizationService _authService;
    private readonly IAgentBus _agentBus;
    private readonly IMediator _mediator;
    private readonly AppDbContext _context;
    private readonly ILogger<GamesController> _logger;

    public GamesController(
        IGameManagementService gameService,
        ISessionManagementService sessionService,
        IPlayerManagementService playerService,
        Adnd.Server.Services.IUserIdProvider userIdProvider,
        IGameAuthorizationService authService,
        IAgentBus agentBus,
        IMediator mediator,
        AppDbContext context,
        ILogger<GamesController> logger)
    {
        _gameService = gameService;
        _sessionService = sessionService;
        _playerService = playerService;
        _userIdProvider = userIdProvider;
        _authService = authService;
        _agentBus = agentBus;
        _mediator = mediator;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// List all games the current user has access to.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetGames()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var games = await _gameService.GetGamesAsync(userId);
        return Ok(games);
    }

    /// <summary>
    /// Get details for a specific game.
    /// </summary>
    /// <param name="id">Game ID.</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetGame(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _gameService.GetGameAsync(id, userId);
        if (game == null) return Forbid();

        return Ok(game);
    }

    /// <summary>
    /// Create a new TTRPG game.
    /// </summary>
    /// <param name="request">Game creation parameters (name, system, LLM preset, plot seed).</param>
    /// <returns>Created game details.</returns>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateGame([FromBody] CreateGameRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var response = await _gameService.CreateGameAsync(userId, request);

        // Publish game created event
        await _mediator.Publish(new GameCreated(response.Id, userId, request.SystemId, request.LLMPresetId));

        return Ok(response);
    }

    /// <summary>
    /// Generate an invite URL/code for the game.
    /// </summary>
    /// <param name="id">Game ID.</param>
    /// <returns>Invite URL and code.</returns>
    [HttpPost("{id}/invite")]
    [Authorize]
    public async Task<IActionResult> GenerateInvite(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        try
        {
            var response = await _gameService.GenerateInviteAsync(id, userId);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Update the narration language for a game.
    /// </summary>
    /// <param name="id">Game ID.</param>
    /// <param name="request">New language setting.</param>
    [HttpPut("{id}/language")]
    [Authorize]
    public async Task<IActionResult> UpdateGameLanguage(Guid id, [FromBody] UpdateGameLanguageRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        try
        {
            await _gameService.UpdateGameLanguageAsync(id, userId, request.Language);
            return Ok(new { id, Language = request.Language ?? "English" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Join a game using an invite code.
    /// </summary>
    /// <param name="request">Invite code.</param>
    /// <returns>Confirmation with gameId.</returns>
    [HttpPost("join-by-code")]
    [Authorize]
    public async Task<IActionResult> JoinByCode([FromBody] JoinByCodeRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null) return Unauthorized();

        try
        {
            var (player, message) = await _playerService.JoinByCodeAsync(request.Code, userIdStr);
            return Ok(new { message, gameId = player.GameId, inviteCode = request.Code.TrimStart('/') });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Join a game by game ID (alternative to invite code).
    /// </summary>
    /// <param name="id">Game ID to join.</param>
    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<IActionResult> JoinGame(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null) return Unauthorized();

        try
        {
            var game = await _context.Games.Include(g => g.Players).FirstOrDefaultAsync(g => g.Id == id);
            if (game == null) return NotFound(new { error = "Game not found." });

            var (player, message) = await _playerService.JoinGameAsync(id, userIdStr, game.Players.Count);
            return Ok(new { message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Leave a game you are currently a player of.
    /// </summary>
    /// <param name="id">Game ID to leave.</param>
    [HttpPost("{id}/leave")]
    [Authorize]
    public async Task<IActionResult> LeaveGame(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null) return Unauthorized();

        try
        {
            await _playerService.LeaveGameAsync(id, userIdStr);
            return Ok(new { message = "Left game successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Game not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a game (creator only).
    /// </summary>
    /// <param name="id">Game ID to delete.</param>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteGame(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        try
        {
            await _gameService.DeleteGameAsync(id, userId);
            return Ok(new { message = "Game deleted successfully." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // ==================== Session Management ====================

    /// <summary>
    /// Get all sessions for a game.
    /// </summary>
    /// <param name="id">Game ID.</param>
    [HttpGet("{id}/sessions")]
    public async Task<IActionResult> GetSessions(Guid id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });

        if (!await _authService.HasAccessAsync(_context, id, _userIdProvider.GetCurrentUserId())) return Forbid();

        var sessions = await _sessionService.GetSessionsAsync(id, _context);
        return Ok(sessions);
    }

    /// <summary>
    /// Create a new session within a game.
    /// </summary>
    /// <param name="id">Game ID.</param>
    /// <param name="request">Session title and description.</param>
    [HttpPost("{id}/sessions")]
    public async Task<IActionResult> CreateSession(Guid id, [FromBody] CreateSessionRequest request)
    {
        var creatorId = _userIdProvider.GetCurrentUserId();

        try
        {
            var (session, response) = await _sessionService.CreateSessionAsync(id, creatorId, request);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Close (end) a game session.
    /// </summary>
    /// <param name="id">Game ID.</param>
    /// <param name="sessionId">Session ID to close.</param>
    [HttpPost("{id}/sessions/{sessionId}/close")]
    public async Task<IActionResult> CloseSession(Guid id, Guid sessionId)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        try
        {
            await _sessionService.CloseSessionAsync(id, sessionId);
            return Ok(new { sessionId, EndedAt = DateTime.UtcNow });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all players in a game.
    /// </summary>
    /// <param name="id">Game ID.</param>
    [HttpGet("{id}/players")]
    public async Task<IActionResult> GetPlayers(Guid id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, id, _userIdProvider.GetCurrentUserId())) return Forbid();

        var players = await _playerService.GetPlayersAsync(id, _context);
        return Ok(players);
    }

    /// <summary>
    /// Promote a player to a different role (Creator, Player, Spectator, Observer).
    /// </summary>
    /// <param name="id">Game ID.</param>
    /// <param name="playerId">Player ID to promote.</param>
    /// <param name="role">New role.</param>
    [HttpPost("{id}/players/{playerId}/promote")]
    public async Task<IActionResult> PromotePlayer(Guid id, Guid playerId, [FromBody] string role)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != _userIdProvider.GetCurrentUserId()) return Forbid();

        try
        {
            await _playerService.PromotePlayerAsync(id, game.CreatorId, playerId, role);
            return Ok(new { playerId, Role = role });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

}

/// <summary>
/// Request to create a new game session.
/// </summary>
public class CreateSessionRequest
{
    /// <summary>Session title.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Optional session description.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request to join a game by invite code.
/// </summary>
public class JoinByCodeRequest
{
    /// <summary>Invite code (8-character alphanumeric).</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a game's narration language.
/// </summary>
public class UpdateGameLanguageRequest
{
    /// <summary>Narration language (e.g., "English", "Spanish", "Japanese").</summary>
    public string Language { get; set; } = "English";
}
