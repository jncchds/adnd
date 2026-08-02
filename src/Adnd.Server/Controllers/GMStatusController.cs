using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[Route("api/[controller]")]
public class GMStatusController(
    IAgentBus agentBus,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpGet("{gameId:guid}")]
    public async Task<IActionResult> GetStatus(Guid gameId)
    {
        if (await RequireMember(gameId) is { } failure) return failure;

        var status = await agentBus.GetGMStatusAsync(gameId);
        return Ok(new { gameId, status });
    }

    // Pausing the GM stops the game for everyone at the table — creator only.
    [HttpPost("{gameId:guid}/pause")]
    public async Task<IActionResult> Pause(Guid gameId)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        await agentBus.PauseGMAsync(gameId);
        return NoContent();
    }

    [HttpPost("{gameId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid gameId)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        await agentBus.ResumeGMAsync(gameId);
        return NoContent();
    }
}
