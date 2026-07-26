using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

public record SwayRequest(Guid GameId, string Direction, int Intensity, string Content);

[ApiController]
[Route("api/sway")]
[Authorize]
public class SwayController(IAgentBus agentBus) : ControllerBase
{
    /// <summary>Sways the story in the specified direction.</summary>
    [HttpPost]
    public async Task<IActionResult> Sway([FromBody] SwayRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Direction))
            return BadRequest("Direction is required.");

        await agentBus.SendSwayAsync(request.GameId, request.Direction, request.Intensity, request.Content);
        return Accepted(new { message = "Story sway dispatched" });
    }
}
