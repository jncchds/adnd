using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Adnd.Server.Shared;
using Adnd.Server.Features.Games.Dto;
using System.Security.Claims;
using System.Text;

namespace Adnd.Server.Features.Games;

[ApiController]
[Route("api/games")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly AppDbContext _db;
    private static readonly string _codes = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public GameController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var games = await _db.Games
            .Include(g => g.System)
            .Include(g => g.Creator)
            .Include(g => g.GamePlayers)
            .Where(g => g.CreatorId == Guid.Parse(userId) || g.GamePlayers.Any(gp => gp.UserId == Guid.Parse(userId)))
            .OrderByDescending(g => g.UpdatedAt)
            .ToListAsync();

        return Ok(games.Select(MapToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGameRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var system = await _db.GameSystems.FindAsync(request.SystemId);
        if (system == null) return NotFound(new { error = "Game system not found" });

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            SystemId = request.SystemId,
            CreatorId = Guid.Parse(userId),
            PlotSeed = request.PlotSeed,
            Status = GameStatus.Draft,
            JoinCode = GenerateJoinCode(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Games.Add(game);

        // Creator is automatically a player with CREATOR role
        var creatorPlayer = new GamePlayer
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            UserId = Guid.Parse(userId),
            Role = PlayerRole.Creator,
            CharacterName = string.Empty,
            Spectating = false,
            IsBanned = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.GamePlayers.Add(creatorPlayer);

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = game.Id }, MapToResponse(game));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games
            .Include(g => g.System)
            .Include(g => g.Creator)
            .Include(g => g.GamePlayers)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null) return NotFound();

        if (game.CreatorId != Guid.Parse(userId) && !game.GamePlayers.Any(gp => gp.UserId == Guid.Parse(userId)))
            return Forbid();

        return Ok(MapToResponse(game));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGameRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id && g.CreatorId == Guid.Parse(userId));
        if (game == null) return NotFound();

        game.Title = request.Title;
        game.PlotSeed = request.PlotSeed;
        if (request.Status != null)
            game.Status = request.Status switch
            {
                "Draft" => GameStatus.Draft,
                "Active" => GameStatus.Active,
                "Archived" => GameStatus.Archived,
                _ => game.Status
            };
        game.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(MapToResponse(game));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id && g.CreatorId == Guid.Parse(userId));
        if (game == null) return NotFound();

        game.Status = GameStatus.Archived;
        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/join")]
    public async Task<IActionResult> Join(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null) return NotFound();
        if (game.Status == GameStatus.Archived) return BadRequest(new { error = "Cannot join archived games" });

        var existing = await _db.GamePlayers.FirstOrDefaultAsync(gp => gp.GameId == id && gp.UserId == Guid.Parse(userId));
        if (existing != null) return Conflict(new { error = "Already a player" });

        var player = new GamePlayer
        {
            Id = Guid.NewGuid(),
            GameId = id,
            UserId = Guid.Parse(userId),
            Role = PlayerRole.Player,
            CharacterName = string.Empty,
            Spectating = false,
            IsBanned = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.GamePlayers.Add(player);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var player = await _db.GamePlayers.FirstOrDefaultAsync(gp => gp.GameId == id && gp.UserId == Guid.Parse(userId));
        if (player == null) return NotFound(new { error = "Not a player" });

        _db.GamePlayers.Remove(player);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("join-by-code")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinByCodeRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games.FirstOrDefaultAsync(g => g.JoinCode == request.JoinCode.ToUpper());
        if (game == null) return NotFound(new { error = "Invalid join code" });
        if (game.Status == GameStatus.Archived) return BadRequest(new { error = "Cannot join archived games" });

        var existing = await _db.GamePlayers.FirstOrDefaultAsync(gp => gp.GameId == game.Id && gp.UserId == Guid.Parse(userId));
        if (existing != null)
        {
            if (existing.IsBanned) return BadRequest(new { error = "You have been banned from this game" });
            return Conflict(new { error = "Already a player" });
        }

        var player = new GamePlayer
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            UserId = Guid.Parse(userId),
            Role = PlayerRole.Player,
            CharacterName = string.Empty,
            Spectating = false,
            IsBanned = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.GamePlayers.Add(player);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/ban/{playerId}")]
    public async Task<IActionResult> BanPlayer(Guid id, Guid playerId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id && g.CreatorId == Guid.Parse(userId));
        if (game == null) return NotFound();

        var player = await _db.GamePlayers.FirstOrDefaultAsync(gp => gp.GameId == id && gp.UserId == playerId);
        if (player == null) return NotFound(new { error = "Player not found" });
        if (player.UserId == game.CreatorId) return BadRequest(new { error = "Cannot ban the creator" });

        player.IsBanned = true;
        player.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/unban/{playerId}")]
    public async Task<IActionResult> UnbanPlayer(Guid id, Guid playerId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id && g.CreatorId == Guid.Parse(userId));
        if (game == null) return NotFound();

        var player = await _db.GamePlayers.FirstOrDefaultAsync(gp => gp.GameId == id && gp.UserId == playerId);
        if (player == null) return NotFound(new { error = "Player not found" });

        player.IsBanned = false;
        player.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/regenerate-code")]
    public async Task<IActionResult> RegenerateJoinCode(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id && g.CreatorId == Guid.Parse(userId));
        if (game == null) return NotFound();

        game.JoinCode = GenerateJoinCode();
        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { joinCode = game.JoinCode });
    }

    private GameResponse MapToResponse(Game game) => new()
    {
        Id = game.Id,
        Title = game.Title,
        SystemId = game.SystemId,
        SystemName = game.System?.Name ?? "Unknown",
        SystemSlug = game.System?.Slug ?? "unknown",
        CreatorId = game.CreatorId,
        CreatorDisplayName = game.Creator?.DisplayName ?? "Unknown",
        Status = game.Status.ToString(),
        PlotSeed = game.PlotSeed,
        JoinCode = game.JoinCode,
        PlayerCount = game.GamePlayers.Count,
        CreatedAt = game.CreatedAt,
        UpdatedAt = game.UpdatedAt
    };

    private static string GenerateJoinCode()
    {
        var sb = new StringBuilder(8);
        for (int i = 0; i < 8; i++)
            sb.Append(_codes[Random.Shared.Next(_codes.Length)]);
        return sb.ToString();
    }
}
