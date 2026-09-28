using Adnd.Server.Hubs;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using IUserIdProvider = Adnd.Server.Services.IUserIdProvider;

namespace Adnd.Server.Controllers;

[Route("api/[controller]")]
public class GMStatusController(
    IAgentBus agentBus,
    IHubContext<GameHub> hub,
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
        // The table's status chip only learns of this through the push; it used to be sent by
        // a hub method nothing called, while this endpoint — the one the UI uses — sent nothing.
        await hub.Clients.Group(gameId.ToString()).SendAsync("GMStatusChanged", new GMStatusDto(gameId, "Paused", null));
        return NoContent();
    }

    [HttpPost("{gameId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid gameId)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        await agentBus.ResumeGMAsync(gameId);
        await hub.Clients.Group(gameId.ToString()).SendAsync("GMStatusChanged", new GMStatusDto(gameId, "Running", null));
        return NoContent();
    }
}
