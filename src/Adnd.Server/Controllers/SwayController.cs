using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

public record SwayRequest(Guid GameId, string Direction, int Intensity, string Content);

[Route("api/sway")]
public class SwayController(
    IAgentBus agentBus,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    /// <summary>Sways the story in the specified direction.</summary>
    [HttpPost]
    public async Task<IActionResult> Sway([FromBody] SwayRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Direction))
            return BadRequest(new { error = "Direction is required." });

        // Injects attacker-controlled direction into the GM's prompt stream, so this has
        // to be scoped to the caller's own games.
        if (await RequireMember(request.GameId) is { } failure) return failure;

        await agentBus.SendSwayAsync(request.GameId, request.Direction, request.Intensity, request.Content);
        return Accepted(new { message = "Story sway dispatched" });
    }
}
