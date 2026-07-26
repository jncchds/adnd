using Adnd.Server.Data;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GMStatusController(IAgentBus agentBus) : ControllerBase
{
    [HttpGet("{gameId}")]
    public async Task<IActionResult> GetStatus(Guid gameId)
    {
        var status = await agentBus.GetGMStatusAsync(gameId);
        return Ok(new { gameId, status });
    }

    [HttpPost("{gameId}/pause")]
    public async Task<IActionResult> Pause(Guid gameId)
    {
        await agentBus.PauseGMAsync(gameId);
        return NoContent();
    }

    [HttpPost("{gameId}/resume")]
    public async Task<IActionResult> Resume(Guid gameId)
    {
        await agentBus.ResumeGMAsync(gameId);
        return NoContent();
    }
}
