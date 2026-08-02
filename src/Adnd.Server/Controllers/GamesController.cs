using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/games")]
[Authorize]
public class GamesController(
    IGameManagementService games,
    IUserIdProvider userIdProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUserGames()
    {
        var userId = userIdProvider.GetUserId();
        var result = await games.GetUserGamesAsync(userId);
        return Ok(result);
    }

    [HttpGet("archived")]
    public async Task<IActionResult> GetArchivedGames()
    {
        var userId = userIdProvider.GetUserId();
        var result = await games.GetArchivedUserGamesAsync(userId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGameDto dto)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var game = await games.CreateAsync(userId, dto);
            return CreatedAtAction(nameof(GetById), new { id = game.Id }, game);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var game = await games.GetByIdAsync(id, userId);
            return Ok(game);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGameDto dto)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var game = await games.UpdateAsync(id, userId, dto);
            return Ok(game);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await games.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/invite")]
    public async Task<IActionResult> GenerateInviteCode(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var code = await games.GenerateInviteCodeAsync(id, userId);
            return Ok(new { inviteCode = code });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("join")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinByCodeRequest dto)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var game = await games.JoinByCodeAsync(dto.InviteCode, userId, dto.CharacterName);
            return Ok(game);
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

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, [FromServices] IGameStartService gameStartService)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await games.StartAsync(id, userId);
            await gameStartService.StartGameAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await games.ArchiveAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/players")]
    public async Task<IActionResult> GetPlayers(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var players = await games.GetPlayersAsync(id, userId);
            return Ok(players.Select(PlayerDto.From));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/players/{playerId:guid}/promote")]
    public async Task<IActionResult> PromotePlayer(Guid id, Guid playerId, [FromBody] PromotePlayerRequest dto)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await games.PromotePlayerAsync(id, playerId, dto.Role, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/players/{playerId:guid}")]
    public async Task<IActionResult> KickPlayer(Guid id, Guid playerId)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await games.KickPlayerAsync(id, playerId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    /// <summary>Sessions for a game, newest first. Members only.</summary>
    [HttpGet("{id:guid}/sessions")]
    public async Task<IActionResult> GetSessions(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var sessions = await games.GetSessionsAsync(id, userId, ct);
            return Ok(sessions.Select(s => new GameSessionDto(
                s.Id, s.GameId, s.Title, s.Description, s.Status, s.CreatedAt, s.ClosedAt)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }
}
