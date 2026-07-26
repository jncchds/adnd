using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WhispersController(AppDbContext db, IWhisperService whisperService, IUserIdProvider userIdProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid sessionId)
    {
        var userId = userIdProvider.GetUserId();
        var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        if (player == null) return Ok(new List<object>());
        return Ok(await whisperService.GetWhisperHistoryAsync(sessionId, player.Id));
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendWhisperRequest req)
    {
        var userId = userIdProvider.GetUserId();
        var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        var whisper = await whisperService.CreateWhisperAsync(req.SessionId, player?.Id, WhisperType.PlayerToPlayer, req.Content, req.TargetPlayerIds);
        return Ok(whisper);
    }
}

public record SendWhisperRequest(Guid SessionId, string Content, List<Guid> TargetPlayerIds);
