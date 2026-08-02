using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[Route("api/[controller]")]
public class WhispersController(
    AppDbContext db,
    IWhisperService whisperService,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid sessionId, CancellationToken ct)
    {
        var (player, failure) = await ResolvePlayerForSessionAsync(sessionId, ct);
        if (failure is not null) return failure;

        return Ok(await whisperService.GetWhisperHistoryAsync(sessionId, player!.Id));
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendWhisperRequest req, CancellationToken ct)
    {
        var (player, failure) = await ResolvePlayerForSessionAsync(req.SessionId, ct);
        if (failure is not null) return failure;

        var whisper = await whisperService.CreateWhisperAsync(
            req.SessionId, player!.Id, WhisperType.PlayerToPlayer, req.Content, req.TargetPlayerIds);

        return Ok(whisper);
    }

    /// <summary>
    /// Resolves the caller's player *in the session's own game*. This previously looked up
    /// the caller's player row with no GameId predicate, so a player in game A could read
    /// and post whispers in a session belonging to game B.
    /// </summary>
    private async Task<(Player? Player, IActionResult? Failure)> ResolvePlayerForSessionAsync(
        Guid sessionId, CancellationToken ct)
    {
        var session = await db.GameSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return (null, NotFound(new { error = "Session not found." }));

        return await ResolvePlayer(session.GameId);
    }
}

public record SendWhisperRequest(Guid SessionId, string Content, List<Guid> TargetPlayerIds);
